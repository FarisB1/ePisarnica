using ePisarnica.Models;
using ePisarnica.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using QRCoder;
using ePisarnica.Helpers;

namespace ePisarnica.Controllers
{
    public class FileManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly QRHelper _qrHelper;

        public FileManagerController(AppDbContext context, IWebHostEnvironment environment, QRHelper qrHelper)
        {
            _context = context;
            _environment = environment;
            _qrHelper = qrHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string folder = null, string view = "grid", int page = 1, int pageSize = 12, string search = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return RedirectToAction("Login", "Account");

            
            var isAdmin = User.IsInRole("Admin");

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 12;

            var model = await GetFileManagerViewModelWithPagination(id, folder, page, pageSize, search, isAdmin);

            ViewBag.CurrentView = view;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = search;
            ViewBag.IsAdmin = isAdmin;

            int? currentFolderId = null;
            if (!string.IsNullOrEmpty(folder) && folder != "all" && !IsSpecialFolder(folder))
            {
                var folderQuery = _context.Folders.Where(f => f.Name == folder);
                if (!isAdmin)
                {
                    folderQuery = folderQuery.Where(f => f.UserId == id);
                }

                var currentFolder = await folderQuery.FirstOrDefaultAsync();
                currentFolderId = currentFolder?.Id;
            }
            ViewBag.CurrentFolderId = currentFolderId;

            return View(model);
        }

        private async Task<FileManagerViewModel> GetFileManagerViewModelWithPagination(int userId, string currentFolder = null, int page = 1, int pageSize = 12, string search = null, bool isAdmin = false)
        {
            var documentsQuery = _context.Documents
                .Include(d => d.Folder)
                .Include(d => d.User)
                .Where(d => !d.IsTrashed);

            if (!isAdmin)
            {
                documentsQuery = documentsQuery.Where(d => d.UserId == userId);
            }

            var trashCountQuery = _context.Documents.Where(d => d.IsTrashed);
            if (!isAdmin)
            {
                trashCountQuery = trashCountQuery.Where(d => d.UserId == userId);
            }
            var trashCount = await trashCountQuery.CountAsync();

            IQueryable<Document> filteredDocuments = documentsQuery;

            if (!string.IsNullOrEmpty(currentFolder) && currentFolder != "all")
            {
                filteredDocuments = currentFolder switch
                {
                    "recent" => documentsQuery.OrderByDescending(d => d.ModifiedAt).Take(50),
                    "images" => documentsQuery.Where(d => d.FileType == FileType.Image),
                    "documents" => documentsQuery.Where(d => d.FileType == FileType.Document),
                    "shared" => documentsQuery.Where(d => d.IsShared),
                    "trash" => isAdmin
                        ? _context.Documents.Include(d => d.User).Where(d => d.IsTrashed)
                        : _context.Documents.Where(d => d.UserId == userId && d.IsTrashed),
                    _ => documentsQuery.Where(d => d.Folder != null && d.Folder.Name == currentFolder)
                };
            }

            if (!string.IsNullOrEmpty(search))
            {
                if (isAdmin)
                {
                    filteredDocuments = filteredDocuments.Where(d =>
                        d.Title.Contains(search) ||
                        d.FileName.Contains(search) ||
                        (d.Folder != null && d.Folder.Name.Contains(search)) ||
                        (d.User != null && d.User.Username.Contains(search))
                    );
                }
                else
                {
                    filteredDocuments = filteredDocuments.Where(d =>
                        d.Title.Contains(search) ||
                        d.FileName.Contains(search) ||
                        (d.Folder != null && d.Folder.Name.Contains(search))
                    );
                }
            }

            var totalFiles = await filteredDocuments.CountAsync();

            var files = await filteredDocuments
                .OrderByDescending(d => d.ModifiedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var foldersQuery = _context.Folders.AsQueryable();
            if (!isAdmin)
            {
                foldersQuery = foldersQuery.Where(f => f.UserId == userId);
            }
            else
            {
                foldersQuery = foldersQuery.Include(f => f.User);
            }

            var folders = await foldersQuery
                .OrderBy(f => f.Name)
                .ToListAsync();

            var recentFilesQuery = documentsQuery.OrderByDescending(d => d.ModifiedAt).Take(5);
            var recentFiles = await recentFilesQuery.ToListAsync();

            var model = new FileManagerViewModel
            {
                Files = files,
                Folders = folders,
                RecentFiles = recentFiles,
                CurrentFolder = currentFolder,
                TrashCount = trashCount
            };

            ViewBag.TotalFiles = totalFiles;

            return model;
        }

        private async Task<FileManagerViewModel> GetFileManagerViewModel(int userId, string currentFolder = null, bool isAdmin = false)
        {
            return await GetFileManagerViewModelWithPagination(userId, currentFolder, 1, 1000, null, isAdmin);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFolder([FromBody] DeleteFolderRequest request)
        {
            if (request == null)
                return Json(new { success = false, message = "Invalid request" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var folderQuery = _context.Folders.Include(f => f.Documents).Where(f => f.Id == request.FolderId);

            if (!isAdmin)
            {
                folderQuery = folderQuery.Where(f => f.UserId == id);
            }

            var folder = await folderQuery.FirstOrDefaultAsync();

            if (folder == null)
                return Json(new { success = false, message = "Folder not found" });

            if (folder.Documents.Any(d => !d.IsTrashed))
            {
                return Json(new { success = false, message = "Cannot delete folder that contains files. Please move or delete all files first." });
            }

            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Folder deleted successfully" });
        }

        public class DeleteFolderRequest
        {
            public int FolderId { get; set; }
        }

        public async Task<IActionResult> UploadFiles(List<IFormFile> files, int? folderId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            if (files == null || files.Count == 0)
                return Json(new { success = false, message = "No files selected" });

            var isAdmin = User.IsInRole("Admin");

            if (folderId.HasValue)
            {
                var folderQuery = _context.Folders.Where(f => f.Id == folderId.Value);

                if (!isAdmin)
                {
                    folderQuery = folderQuery.Where(f => f.UserId == id);
                }

                var folder = await folderQuery.FirstOrDefaultAsync();
                if (folder == null)
                    return Json(new { success = false, message = "Invalid folder" });
            }

            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", id.ToString());
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var uploadedFilesCount = 0;

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var document = new Document
                    {
                        Title = Path.GetFileNameWithoutExtension(file.FileName),
                        FileName = file.FileName,
                        FilePath = $"/uploads/{id}/{fileName}",
                        FileSize = file.Length,
                        FileExtension = Path.GetExtension(file.FileName).ToLower(),
                        FileType = GetFileType(Path.GetExtension(file.FileName)),
                        UserId = id,
                        FolderId = folderId,
                        CreatedAt = DateTime.Now,
                        ModifiedAt = DateTime.Now
                    };

                    _context.Documents.Add(document);
                    uploadedFilesCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"{uploadedFilesCount} file(s) uploaded successfully",
                count = uploadedFilesCount
            });
        }

        public class FolderCreateRequest
        {
            public string Name { get; set; }
            public string Color { get; set; } = "#6c757d";
        }

        [HttpPost]
        public async Task<IActionResult> CreateFolder([FromBody] FolderCreateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return Json(new { success = false, message = "Folder name is required" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var existingFolder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Name.ToLower() == request.Name.ToLower() && f.UserId == id);

            if (existingFolder != null)
                return Json(new { success = false, message = "A folder with this name already exists" });

            var folder = new Folder
            {
                Name = request.Name,
                Color = request.Color ?? "#6c757d",
                UserId = id,
                CreatedAt = DateTime.Now
            };

            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();

            return Json(new { success = true, folder = folder });
        }

        public class EditFolderRequest
        {
            public int FolderId { get; set; }
            public string NewName { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> EditFolder([FromBody] EditFolderRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewName))
                return Json(new { success = false, message = "Folder name is required" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var folderQuery = _context.Folders.Where(f => f.Id == request.FolderId);

            if (!isAdmin)
            {
                folderQuery = folderQuery.Where(f => f.UserId == id);
            }

            var folder = await folderQuery.FirstOrDefaultAsync();

            if (folder == null)
                return Json(new { success = false, message = "Folder not found" });

            var targetUserId = isAdmin ? folder.UserId : id;
            var existingFolder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Name.ToLower() == request.NewName.ToLower() &&
                                    f.UserId == targetUserId && f.Id != request.FolderId);

            if (existingFolder != null)
                return Json(new { success = false, message = "A folder with this name already exists" });

            folder.Name = request.NewName.Trim();
            _context.Update(folder);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Folder renamed successfully", folder = folder });
        }

        public class EditFileRequest
        {
            public int FileId { get; set; }
            public string NewName { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> EditFile([FromBody] EditFileRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewName))
                return Json(new { success = false, message = "File name is required" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            
            var fileQuery = _context.Documents.Where(d => d.Id == request.FileId);

            
            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(d => d.UserId == id);
            }

            var file = await fileQuery.FirstOrDefaultAsync();

            if (file == null)
                return Json(new { success = false, message = "File not found" });

         
            var invalidChars = Path.GetInvalidFileNameChars();
            if (request.NewName.IndexOfAny(invalidChars) >= 0)
                return Json(new { success = false, message = "File name contains invalid characters" });

            
            var newNameWithoutExtension = Path.GetFileNameWithoutExtension(request.NewName.Trim());
            var originalExtension = file.FileExtension;

            file.Title = newNameWithoutExtension;
            file.FileName = newNameWithoutExtension + originalExtension;
            file.ModifiedAt = DateTime.Now;

            _context.Update(file);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "File renamed successfully",
                file = new
                {
                    id = file.Id,
                    title = file.Title,
                    fileName = file.FileName
                }
            });
        }

        
        public class MoveFileRequest
        {
            public int FileId { get; set; }
            public int? TargetFolderId { get; set; } 
        }

        [HttpPost]
        public async Task<IActionResult> MoveFile([FromBody] MoveFileRequest request)
        {
            if (request == null)
                return Json(new { success = false, message = "Invalid request" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var fileQuery = _context.Documents.Where(f => f.Id == request.FileId);

            
            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(f => f.UserId == id);
            }

            var file = await fileQuery.FirstOrDefaultAsync();
            if (file == null)
                return Json(new { success = false, message = "File not found" });

            if (request.TargetFolderId.HasValue)
            {
                var folderQuery = _context.Folders.Where(f => f.Id == request.TargetFolderId.Value);

                if (!isAdmin)
                {
                    folderQuery = folderQuery.Where(f => f.UserId == id);
                }

                var folder = await folderQuery.FirstOrDefaultAsync();
                if (folder == null)
                    return Json(new { success = false, message = "Target folder not found" });
            }

            file.FolderId = request.TargetFolderId;
            file.ModifiedAt = DateTime.Now;

            _context.Update(file);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "File moved successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> MoveToTrash([FromBody] MoveToTrashViewModel model)
        {
            if (model == null) return Json(new { success = false, message = "Invalid request" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var fileQuery = _context.Documents.Where(d => d.Id == model.Id);

            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(d => d.UserId == id);
            }

            var file = await fileQuery.FirstOrDefaultAsync();
            if (file == null)
            {
                return Json(new { success = false, message = "File not found" });
            }

            file.IsTrashed = true;
            file.TrashedAt = DateTime.Now;
            file.ModifiedAt = DateTime.Now;

            _context.Update(file);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class MoveToTrashViewModel
        {
            public int Id { get; set; }
        }

        public async Task<IActionResult> Trash()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            var trashedFilesQuery = _context.Documents.Include(d => d.User).Where(d => d.IsTrashed);

            if (!isAdmin)
            {
                trashedFilesQuery = trashedFilesQuery.Where(d => d.UserId == int.Parse(userId));
            }

            var trashedFiles = await trashedFilesQuery.ToListAsync();

            ViewBag.IsAdmin = isAdmin;
            return View("Trash", trashedFiles);
        }

        [HttpPost]
        public async Task<IActionResult> DeletePermanent(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            var fileQuery = _context.Documents.Where(d => d.Id == id);

            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(d => d.UserId == int.Parse(userId));
            }

            var file = await fileQuery.FirstOrDefaultAsync();
            if (file == null) return NotFound();

            bool physicalDeleted = DeletePhysicalFile(file.FilePath);

            _context.Documents.Remove(file);
            await _context.SaveChangesAsync();

            if (!physicalDeleted)
            {
                TempData["Warning"] = $"File '{file.FileName}' was removed from your account, but the physical file could not be deleted.";
            }

            return RedirectToAction("Trash");
        }

        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            var fileQuery = _context.Documents.Where(d => d.Id == id);

            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(d => d.UserId == int.Parse(userId));
            }

            var file = await fileQuery.FirstOrDefaultAsync();
            if (file == null) return NotFound();

            file.IsTrashed = false;
            file.TrashedAt = null;
            file.ModifiedAt = DateTime.Now;

            _context.Update(file);
            await _context.SaveChangesAsync();

            return RedirectToAction("Trash");
        }

        private bool IsSpecialFolder(string folder)
        {
            var specialFolders = new[] { "recent", "images", "documents", "shared", "trash" };
            return specialFolders.Contains(folder?.ToLower());
        }

        private FileType GetFileType(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => FileType.Image,
                ".pdf" or ".doc" or ".docx" or ".txt" or ".xls" or ".xlsx" => FileType.Document,
                ".mp3" or ".wav" or ".ogg" => FileType.Audio,
                ".mp4" or ".mov" or ".avi" => FileType.Video,
                ".zip" or ".rar" or ".7z" => FileType.Archive,
                _ => FileType.Other
            };
        }

        [HttpGet]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var isAdmin = User.IsInRole("Admin");

            var documentQuery = _context.Documents.Where(d => d.Id == id);

            if (!isAdmin)
            {
                documentQuery = documentQuery.Where(d => d.UserId.ToString() == userId);
            }

            var document = await documentQuery.FirstOrDefaultAsync();

            if (document == null)
                return NotFound();

            var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, GetMimeType(document.FileExtension), document.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> OpenFile(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var isAdmin = User.IsInRole("Admin");

            // Query document
            var documentQuery = _context.Documents.Where(d => d.Id == id);

            if (!isAdmin)
                documentQuery = documentQuery.Where(d => d.UserId.ToString() == userId);

            var document = await documentQuery.FirstOrDefaultAsync();
            if (document == null)
                return NotFound();

            var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // Set content type based on file extension
            string contentType = GetMimeType(document.FileExtension);

            // For Word files, force download
            if (document.FileExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
                document.FileExtension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
            {
                return File(fileBytes, contentType, Path.GetFileName(filePath));
            }

            // For PDFs or other files, you can display in-browser
            return File(fileBytes, contentType);
        }



        [HttpGet]
        public async Task<IActionResult> ViewFile(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var isAdmin = User.IsInRole("Admin");

            var documentQuery = _context.Documents.Include(d => d.User).Where(d => d.Id == id);

            if (!isAdmin)
            {
                documentQuery = documentQuery.Where(d => d.UserId.ToString() == userId);
            }

            var document = await documentQuery.FirstOrDefaultAsync();

            if (document == null)
                return NotFound();

            ViewBag.IsAdmin = isAdmin;
            return View(document);
        }

        private bool DeletePhysicalFile(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting file {filePath}: {ex.Message}");
                return false;
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request?.FileIds == null || !request.FileIds.Any())
                return Json(new { success = false, message = "No files selected" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var filesQuery = _context.Documents.Where(d => request.FileIds.Contains(d.Id) && d.IsTrashed);

            if (!isAdmin)
            {
                filesQuery = filesQuery.Where(d => d.UserId == id);
            }

            var files = await filesQuery.ToListAsync();

            if (!files.Any())
                return Json(new { success = false, message = "No valid files found for deletion" });

            int deletedCount = 0;
            var failedDeletions = new List<string>();

            foreach (var file in files)
            {
                bool physicalDeleted = DeletePhysicalFile(file.FilePath);

                _context.Documents.Remove(file);
                deletedCount++;

                if (!physicalDeleted)
                {
                    failedDeletions.Add(file.FileName);
                }
            }

            await _context.SaveChangesAsync();

            var message = $"{deletedCount} file(s) deleted successfully";
            if (failedDeletions.Any())
            {
                message += $". Warning: Could not delete physical files: {string.Join(", ", failedDeletions)}";
            }

            return Json(new { success = true, message = message });
        }

        [HttpPost]
        public async Task<IActionResult> AddToProtocol([FromBody] AddToProtocolRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var fileQuery = _context.Documents.Where(d => d.Id == request.FileId);

            if (!isAdmin)
            {
                fileQuery = fileQuery.Where(d => d.UserId == id);
            }

            var file = await fileQuery.FirstOrDefaultAsync();
            if (file == null)
                return Json(new { success = false, message = "File not found" });

            var maxBroj = await _context.ProtocolEntries.MaxAsync(p => (int?)p.BrojProtokola) ?? 0;

            var entry = new ProtocolEntry
            {
                BrojProtokola = maxBroj + 1,
                Datum = DateTime.Now,
                Stranka = request.Stranka,
                Napomena = request.Napomena,

                DocumentId = file.Id
            };

            var docName = await _context.ProtocolEntries.Where(d => d.DocumentId == file.Id).Select(d => d.OriginalFileName).FirstOrDefaultAsync();

            entry.QrCodePath = _qrHelper.GenerateQrCode(
                $"Protokol: {entry.BrojProtokola}\n Ime dokumenta: {docName}\nDatum: {entry.Datum:dd.MM.yyyy}",
                entry.BrojProtokola
            );

            _context.ProtocolEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Json(new { success = true, brojProtokola = entry.BrojProtokola });
        }

        public class AddToProtocolRequest
        {
            public int FileId { get; set; }
            public string Stranka { get; set; }
            public string? Napomena { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetDocumentsForProtocol()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var id))
                return Json(new { success = false, message = "Not authenticated" });

            var isAdmin = User.IsInRole("Admin");

            var documentsQuery = _context.Documents
                .Where(d => !d.IsTrashed && d.ProtocolEntry == null);

            if (!isAdmin)
            {
                documentsQuery = documentsQuery.Where(d => d.UserId == id);
            }
            else
            {
                documentsQuery = documentsQuery.Include(d => d.User);
            }

            var documents = await documentsQuery
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Title,
                    fileName = d.FileName,
                    fileType = d.FileType.ToString(),
                    userName = isAdmin ? d.User.Username : null
                })
                .ToListAsync();

            return Json(new { success = true, documents });
        }

        public class BulkDeleteRequest
        {
            public List<int> FileIds { get; set; } = new List<int>();
        }

        private string GetMimeType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".mp4" => "video/mp4",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                _ => "application/octet-stream"
            };
        }
    }
}
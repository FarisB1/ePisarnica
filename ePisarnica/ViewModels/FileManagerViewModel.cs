using ePisarnica.Models;
using System.Collections.Generic;

namespace ePisarnica.ViewModels
{
    public class FileManagerViewModel
    {
        public List<Document> Files { get; set; } = new List<Document>();
        public List<Folder> Folders { get; set; } = new List<Folder>();
        public List<Document> RecentFiles { get; set; } = new List<Document>();
        public string CurrentFolder { get; set; }

        public int RecentFilesCount => RecentFiles.Count;
        public int ImagesCount => Files.Count(f => f.FileType == FileType.Image);
        public int DocumentsCount => Files.Count(f => f.FileType == FileType.Document);
        public int SharedCount => Files.Count(f => f.IsShared);
        public int TrashCount { get; set; } 

        public int GetFolderFileCount(int folderId) => Files.Count(f => f.FolderId == folderId);
    }
}
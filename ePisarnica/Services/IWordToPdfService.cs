namespace ePisarnica.Services
{
    public interface IWordToPdfService
    {
        Task<byte[]> ConvertWordToPdfAsync(byte[] wordBytes);
        bool IsWordDocument(string fileName);
    }
}

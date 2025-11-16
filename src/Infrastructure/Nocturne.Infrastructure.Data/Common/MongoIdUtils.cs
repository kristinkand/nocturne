namespace Nocturne.Infrastructure.Data.Common
{
    public static class MongoIdUtils
    {
        public static bool IsValidMongoId(string? id)
        {
            // MongoDB ObjectId is a 24-character hex string
            return !string.IsNullOrEmpty(id)
                && id.Length == 24
                && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{24}$");
        }
    }
}

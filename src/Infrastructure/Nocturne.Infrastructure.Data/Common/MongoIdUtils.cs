namespace Nocturne.Infrastructure.Data.Common
{
    /// <summary>
    /// Utility methods for working with MongoDB ObjectIds
    /// </summary>
    public static class MongoIdUtils
    {
        /// <summary>
        /// Validates if a string is a valid MongoDB ObjectId (24-character hex string)
        /// </summary>
        /// <param name="id">The string to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidMongoId(string? id)
        {
            // MongoDB ObjectId is a 24-character hex string
            return !string.IsNullOrEmpty(id)
                && id.Length == 24
                && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{24}$");
        }
    }
}

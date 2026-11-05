// Models/CatalogValidationIssue.cs
namespace CatalogManager.Services
{
    /// <summary>
    /// A catalog entry item that references a prototype which does not exist in the loaded game data.
    /// </summary>
    public class CatalogValidationIssue
    {
        public ulong SkuId { get; set; }
        public string Title { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public ulong PrototypeId { get; set; }
    }
}

public class CatalogEntry
{
    public ulong SkuId { get; set; }
    public List<GuidItem> GuidItems { get; set; } = new();
    public List<GuidItem> AdditionalGuidItems { get; set; } = new();
    public List<LocalizedEntry> LocalizedEntries { get; set; } = new();
    public List<InfoUrl> InfoUrls { get; set; } = new();
    public List<ContentData> ContentData { get; set; } = new();
    public ItemType Type { get; set; }
    public List<TypeModifier> TypeModifiers { get; set; } = new();
}

public class InfoUrl
{
    public string Language { get; set; }
    public string Url { get; set; }
}

public class ContentData
{
}

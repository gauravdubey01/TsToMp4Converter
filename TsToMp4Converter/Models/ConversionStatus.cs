namespace TsToMp4Converter.Models;

public enum ConversionStatus
{
    Queued,
    Converting,
    Completed,
    Failed,
    Cancelled
}

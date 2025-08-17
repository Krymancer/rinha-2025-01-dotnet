namespace Api.Models
{
  public class JobConfig
  {
    public int? ProcessingBatchSize { get; set; }
    public int? ProcessingParalellism { get; set; }
    public int? ProcessingWait { get; set; }
    public int? PersistenceBatchSize { get; set; }
    public int? PersistenceWait { get; set; }
  }
}
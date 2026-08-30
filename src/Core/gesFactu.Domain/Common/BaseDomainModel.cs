namespace gesFactu.Domain.Common;

public abstract class BaseDomainModel
{
    public int Id { get; set; }
    public DateTime? CreateDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }

    /// <summary>
    /// Soporte de borrado lógico para entidades administrativas.
    /// Los registros fiscales se protegen adicionalmente contra cualquier borrado.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

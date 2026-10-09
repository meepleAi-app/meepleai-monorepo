namespace Api.SharedKernel.Application.Interfaces;

/// <summary>Un integration event con versione di contratto esplicita sul filo (proprietà di messaggio e campo JSON).</summary>
internal interface IVersionedContract
{
    string ContractVersion { get; }
}

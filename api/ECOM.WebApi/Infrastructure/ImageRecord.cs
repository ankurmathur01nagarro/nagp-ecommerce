using System.ComponentModel;

namespace ECOM.WebApi.Infrastructure;

[ImmutableObject(true)]
public sealed record ImageRecord(Guid Id, string Url, string? Alt);

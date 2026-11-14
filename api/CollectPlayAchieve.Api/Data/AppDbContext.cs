using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CollectPlayAchieve.Api.Data;

[SuppressMessage(
    "Design",
    "CA1515",
    Justification = "EF Core tooling discovers the application's DbContext by its public type."
)]
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options) { }

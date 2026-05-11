using Domain;
using Domain.Examples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Examples;

internal sealed class ExampleConfiguration : IEntityTypeConfiguration<Example>
{
    public void Configure(EntityTypeBuilder<Example> builder)
    {
        builder.ToTable("examples");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(SystemConstants.NameMaxLength)
            .IsRequired();

        builder.Ignore(e => e.DomainEvents);
    }
}

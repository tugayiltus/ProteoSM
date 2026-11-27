using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Catalog;
using ProteoSM.Domain.Knowledge;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class ArticleRevisionConfiguration : IEntityTypeConfiguration<ArticleRevision>
{
    public void Configure(EntityTypeBuilder<ArticleRevision> builder)
    {
        builder.ToTable("article_revisions", table =>
            table.HasCheckConstraint("ck_article_revisions_visibility", SnakeCaseEnumConversion<ArticleVisibility>.CheckSql("visibility")));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Title).HasMaxLength(ArticleLimits.TitleMaxLength).IsRequired();
        builder.Property(r => r.Summary).HasMaxLength(ArticleLimits.SummaryMaxLength).IsRequired();
        builder.Property(r => r.Body).HasMaxLength(ArticleLimits.BodyMaxLength).IsRequired();
        builder.Property(r => r.Visibility).HasEnumName();

        builder.HasIndex(r => new { r.ArticleId, r.Revision }).IsUnique();
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.EditedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

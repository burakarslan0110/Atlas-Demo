using OpenSearch.Client;
using ProductService.Models;
using Atlas.Common.DTOs;

namespace ProductService.Services;

public class OpenSearchIndexer : IOpenSearchIndexer
{
    private readonly OpenSearchClient _client;
    private readonly ILogger<OpenSearchIndexer> _logger;
    private const string IndexName = "products";

    public OpenSearchIndexer(OpenSearchClient client, ILogger<OpenSearchIndexer> logger)
    {
        _client = client;
        _logger = logger;
        InitializeIndex();
    }

    private void InitializeIndex()
    {
        var indexExists = _client.Indices.Exists(IndexName).Exists;

        if (!indexExists)
        {
            var createIndexResponse = _client.Indices.Create(IndexName, c => c
                .Map<ProductSearchDocument>(m => m
                    .Properties(p => p
                        .Text(t => t.Name(n => n.Name).Analyzer("standard"))
                        .Text(t => t.Name(n => n.Description).Analyzer("standard"))
                        .Keyword(k => k.Name(n => n.Slug))
                        .Number(n => n.Name(x => x.Price).Type(NumberType.ScaledFloat).ScalingFactor(100))
                        .Number(n => n.Name(x => x.Stock))
                        .Keyword(k => k.Name(n => n.CategoryId))
                        .Text(t => t.Name(n => n.CategoryName))
                        .Keyword(k => k.Name(n => n.CategoryPath))
                        .Text(t => t.Name(n => n.Brand))
                        .Keyword(k => k.Name(n => n.Tags))
                        .Keyword(k => k.Name(n => n.Images))
                        .Number(n => n.Name(x => x.AvgRating).Type(NumberType.Double))
                        .Number(n => n.Name(x => x.ReviewCount))
                        .Boolean(b => b.Name(n => n.IsActive))
                        .Boolean(b => b.Name(n => n.IsFeatured))
                        .Date(d => d.Name(n => n.CreatedAt))
                        .Date(d => d.Name(n => n.UpdatedAt))
                    )
                )
            );

            if (createIndexResponse.IsValid)
            {
                _logger.LogInformation("OpenSearch index created successfully");
            }
            else
            {
                _logger.LogError("Failed to create OpenSearch index: {Error}", createIndexResponse.DebugInformation);
            }
        }
    }

    public async Task IndexProductAsync(Product product)
    {
        var document = MapToSearchDocument(product);
        var response = await _client.IndexDocumentAsync(document);

        if (!response.IsValid)
        {
            _logger.LogError("Failed to index product {ProductId}: {Error}", product.Id, response.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Product {ProductId} indexed successfully", product.Id);
        }
    }

    public async Task UpdateProductAsync(Product product)
    {
        await IndexProductAsync(product);
    }

    public async Task DeleteProductAsync(string productId)
    {
        var response = await _client.DeleteAsync<ProductSearchDocument>(productId, d => d.Index(IndexName));

        if (!response.IsValid && response.Result != Result.NotFound)
        {
            _logger.LogError("Failed to delete product {ProductId} from index: {Error}", productId, response.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Product {ProductId} deleted from index", productId);
        }
    }

    public async Task<ProductSearchResponse> SearchProductsAsync(ProductSearchRequest request)
    {
        var searchDescriptor = new SearchDescriptor<ProductSearchDocument>()
            .Index(IndexName)
            .From((request.Page - 1) * request.PageSize)
            .Size(request.PageSize)
            .Query(q =>
            {
                var queries = new List<Func<QueryContainerDescriptor<ProductSearchDocument>, QueryContainer>>();


                if (!string.IsNullOrEmpty(request.Query))
                {
                    var queryLength = request.Query.Length;
                    var queryLower = request.Query.ToLower();

                    queries.Add(q => q.Bool(b => b
                        .Should(

                            sh => sh.Wildcard(w => w
                                .Field(p => p.Name)
                                .Value($"*{queryLower}*")
                                .Boost(3.0)
                            ),
                            sh => sh.Wildcard(w => w
                                .Field(p => p.Brand)
                                .Value($"*{queryLower}*")
                                .Boost(2.5)
                            ),

                            queryLength >= 4 ? sh => sh.MultiMatch(mm => mm
                                .Fields(f => f
                                    .Field(p => p.Name, boost: 2.0)
                                    .Field(p => p.Description, boost: 1.0)
                                    .Field(p => p.Brand, boost: 1.5)
                                    .Field(p => p.Tags, boost: 1.0)
                                )
                                .Query(request.Query)
                                .Fuzziness(Fuzziness.Auto)
                            ) : null,

                            sh => sh.MultiMatch(mm => mm
                                .Fields(f => f
                                    .Field(p => p.Name, boost: 5.0)
                                    .Field(p => p.Brand, boost: 4.0)
                                )
                                .Query(request.Query)
                                .Type(TextQueryType.Phrase)
                            )
                        )
                        .MinimumShouldMatch(1)
                    ));
                }


                if (!string.IsNullOrEmpty(request.CategoryId))
                {
                    queries.Add(fq => fq.Term(t => t.Field(p => p.CategoryId).Value(request.CategoryId)));
                }


                if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
                {
                    queries.Add(fq => fq.Range(r =>
                    {
                        var range = r.Field(p => p.Price);
                        if (request.MinPrice.HasValue) range = range.GreaterThanOrEquals((double)request.MinPrice.Value);
                        if (request.MaxPrice.HasValue) range = range.LessThanOrEquals((double)request.MaxPrice.Value);
                        return range;
                    }));
                }


                if (!string.IsNullOrEmpty(request.Brand))
                {
                    queries.Add(fq => fq.Term(t => t.Field(p => p.Brand).Value(request.Brand)));
                }


                if (request.Tags != null && request.Tags.Any())
                {
                    queries.Add(fq => fq.Terms(t => t.Field(p => p.Tags).Terms(request.Tags)));
                }


                queries.Add(fq => fq.Term(t => t.Field(p => p.IsActive).Value(true)));

                return q.Bool(b => b.Must(queries.ToArray()));
            })
            .Sort(s =>
            {
                return request.SortBy switch
                {
                    "price_asc" => s.Ascending(p => p.Price),
                    "price_desc" => s.Descending(p => p.Price),
                    "rating" => s.Descending(p => p.AvgRating),
                    "newest" => s.Descending(p => p.CreatedAt),
                    _ => s.Descending(SortSpecialField.Score)
                };
            });

        var response = await _client.SearchAsync<ProductSearchDocument>(searchDescriptor);

        if (!response.IsValid)
        {
            _logger.LogError("Search failed: {Error}", response.DebugInformation);
            return new ProductSearchResponse();
        }

        var products = response.Documents.Select(MapToProductDto).ToList();
        var totalCount = (int)response.Total;

        return new ProductSearchResponse
        {
            Products = products,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }

    private ProductSearchDocument MapToSearchDocument(Product product)
    {
        return new ProductSearchDocument
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            CategoryId = product.Category?.Id ?? string.Empty,
            CategoryName = product.Category?.Name ?? string.Empty,
            CategoryPath = product.Category?.Path ?? new List<string>(),
            Brand = product.Brand,
            Tags = product.Tags,
            Images = product.Images,
            AvgRating = product.AvgRating,
            ReviewCount = product.ReviewCount,
            IsActive = product.IsActive,
            IsFeatured = product.IsFeatured,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    private ProductDto MapToProductDto(ProductSearchDocument doc)
    {
        return new ProductDto
        {
            Id = doc.Id,
            Name = doc.Name,
            Slug = doc.Slug,
            Description = doc.Description,
            Price = doc.Price,
            Stock = doc.Stock,
            Category = new Atlas.Common.DTOs.CategoryInfo
            {
                Id = doc.CategoryId,
                Name = doc.CategoryName,
                Path = doc.CategoryPath
            },
            Brand = doc.Brand,
            Tags = doc.Tags,
            Images = doc.Images,
            AvgRating = doc.AvgRating,
            ReviewCount = doc.ReviewCount,
            IsActive = doc.IsActive,
            IsFeatured = doc.IsFeatured,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }
}

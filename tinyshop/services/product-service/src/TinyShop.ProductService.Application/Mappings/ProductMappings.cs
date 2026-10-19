using TinyShop.ProductService.Application.Models;
using TinyShop.ProductService.Domain.Entities;

namespace TinyShop.ProductService.Application.Mappings;

internal static class ProductMappings
{
    public static ProductDto ToDto(this Product product) =>
        new(product.Id, product.Name, product.PriceCents);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GPInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpOptions("types")]
    public IActionResult OptionsProductTypes()
    {
        return Ok();
    }

    /// <summary>
    /// Obtiene todos los tipos de productos disponibles
    /// </summary>
    /// <returns>Lista de tipos de productos</returns>
    /// <response code="200">Lista de tipos de productos obtenida exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("types")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetProductTypes()
    {
        try
        {
            _logger.LogInformation("Obteniendo tipos de productos");

            var productTypes = await _context.ProductTypes
                .Where(pt => !string.IsNullOrEmpty(pt.Name))
                .Select(pt => new { id = pt.Id, name = pt.Name })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {productTypes.Count} tipos de productos");
            return Ok(productTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tipos de productos");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo tipo de producto/categoría
    /// </summary>
    /// <param name="request">Datos del nuevo tipo de producto</param>
    /// <returns>Tipo de producto creado</returns>
    /// <response code="201">Tipo de producto creado exitosamente</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="409">El tipo de producto ya existe</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost("types")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> CreateProductType([FromBody] CreateProductTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "El nombre de la categoría es requerido" });
            }

            var categoryName = request.Name.Trim();

            _logger.LogInformation($"Creando nuevo tipo de producto: {categoryName}");

            // Verificar si ya existe la categoría
            var existingCategory = await _context.ProductTypes
                .FirstOrDefaultAsync(pt => pt.Name.ToLower() == categoryName.ToLower());

            if (existingCategory != null)
            {
                return Conflict(new { message = "La categoría ya existe" });
            }

            // Crear nueva categoría usando Entity Framework
            var newProductType = new GPInventory.Domain.Entities.ProductType
            {
                Name = categoryName
            };

            _context.ProductTypes.Add(newProductType);
            await _context.SaveChangesAsync();

            var result = new { name = categoryName };

            _logger.LogInformation($"Tipo de producto creado exitosamente: {categoryName}");
            return CreatedAtAction(nameof(GetProductTypes), result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear tipo de producto");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los tipos de productos (endpoint público para testing)
    /// </summary>
    /// <returns>Lista de tipos de productos</returns>
    /// <response code="200">Lista de tipos de productos obtenida exitosamente</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("types/public")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetProductTypesPublic()
    {
        try
        {
            _logger.LogInformation("Obteniendo tipos de productos (público)");

            var productTypes = await _context.ProductTypes
                .Where(pt => !string.IsNullOrEmpty(pt.Name))
                .Select(pt => new { id = pt.Id, name = pt.Name })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {productTypes.Count} tipos de productos");
            return Ok(productTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener tipos de productos");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los productos con filtros opcionales
    /// </summary>
    /// <param name="businessId">ID del negocio (opcional)</param>
    /// <param name="productTypeId">ID del tipo de producto (opcional)</param>
    /// <param name="search">Búsqueda por nombre (opcional)</param>
    /// <returns>Lista de productos</returns>
    /// <response code="200">Lista de productos obtenida exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetProducts(
        [FromQuery] int? businessId = null,
        [FromQuery] int? productTypeId = null,
        [FromQuery] string? search = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo productos con filtros: businessId={businessId}, productTypeId={productTypeId}, search={search}", businessId, productTypeId, search);

            var query = _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .AsQueryable();

            if (businessId.HasValue)
            {
                query = query.Where(p => p.BusinessId == businessId.Value);
            }

            if (productTypeId.HasValue)
            {
                query = query.Where(p => p.ProductTypeId == productTypeId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search));
            }

            var products = await query
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    cost = p.Cost,
                    image = p.Image,
                    date = p.Date,
                    productType = new { id = p.ProductType.Id, name = p.ProductType.Name },
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {products.Count} productos");
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener productos");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene un producto específico por ID
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <returns>Producto encontrado</returns>
    /// <response code="200">Producto encontrado exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="404">Producto no encontrado</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetProduct(int id)
    {
        try
        {
            _logger.LogInformation("Obteniendo producto con ID: {id}", id);

            var product = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    cost = p.Cost,
                    image = p.Image,
                    date = p.Date,
                    productType = new { id = p.ProductType.Id, name = p.ProductType.Name },
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return NotFound(new { message = "Producto no encontrado" });
            }

            _logger.LogInformation("Producto encontrado: {productName}", product.name);
            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener producto con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo producto
    /// </summary>
    /// <param name="request">Datos del nuevo producto</param>
    /// <returns>Producto creado</returns>
    /// <response code="201">Producto creado exitosamente</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "El nombre del producto es requerido" });
            }

            if (request.ProductTypeId <= 0)
            {
                return BadRequest(new { message = "El tipo de producto es requerido" });
            }

            if (request.BusinessId <= 0)
            {
                return BadRequest(new { message = "El negocio es requerido" });
            }

            _logger.LogInformation("Creando nuevo producto: {productName}", request.Name);
            _logger.LogInformation("Request completo - ProductTypeId: {productTypeId}, BusinessId: {businessId}, Name: {name}", 
                request.ProductTypeId, request.BusinessId, request.Name);

            // Verificar que el tipo de producto existe
            _logger.LogInformation("Verificando existencia del tipo de producto con ID: {productTypeId}", request.ProductTypeId);
            
            // Debug: Mostrar todos los ProductTypes disponibles en la base de datos real
            var allProductTypesFromDB = await _context.ProductTypes
                .AsNoTracking()
                .Select(pt => new { pt.Id, pt.Name })
                .ToListAsync();
                
            _logger.LogInformation("ProductTypes disponibles en la base de datos: {@productTypes}", allProductTypesFromDB);
            _logger.LogInformation("Total de ProductTypes en la base de datos: {count}", allProductTypesFromDB.Count);
            
            var productTypeExists = await _context.ProductTypes
                .AsNoTracking()
                .AnyAsync(pt => pt.Id == request.ProductTypeId);
                
            _logger.LogInformation("Verificando ProductTypeId {productTypeId} - Existe: {exists}", request.ProductTypeId, productTypeExists);
            
            // Debug adicional: verificar el tipo específico que se busca
            var specificProductType = await _context.ProductTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.Id == request.ProductTypeId);
                
            if (specificProductType != null)
            {
                _logger.LogInformation("ProductType encontrado en DB: ID={id}, Name={name}", specificProductType.Id, specificProductType.Name);
            }
            else
            {
                _logger.LogWarning("ProductType con ID {id} NO ENCONTRADO en la base de datos. IDs válidos: {validIds}", 
                    request.ProductTypeId, allProductTypesFromDB.Select(pt => pt.Id).ToArray());
            }
            
            if (!productTypeExists)
            {
                _logger.LogError("PRODUCTO NO CREADO: ProductType con ID {productTypeId} no existe en la base de datos", request.ProductTypeId);
                return BadRequest(new { message = "El tipo de producto especificado no existe" });
            }

            // Verificar que el negocio existe
            var businessExists = await _context.Businesses.AnyAsync(b => b.Id == request.BusinessId);
            if (!businessExists)
            {
                return BadRequest(new { message = "El negocio especificado no existe" });
            }

            // Verificar SKU único si se proporciona
            if (!string.IsNullOrEmpty(request.Sku))
            {
                var skuExists = await _context.Products.AnyAsync(p => p.Sku == request.Sku);
                if (skuExists)
                {
                    return BadRequest(new { message = "El SKU ya existe" });
                }
            }

            var newProduct = new GPInventory.Domain.Entities.Product
            {
                Name = request.Name.Trim(),
                Sku = request.Sku?.Trim(),
                Price = request.Price,
                Cost = request.Cost,
                Image = request.Image?.Trim(),
                ProductTypeId = request.ProductTypeId,
                BusinessId = request.BusinessId,
                Date = DateTime.UtcNow
            };

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync();

            // Obtener el producto creado con sus relaciones
            var createdProduct = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.Id == newProduct.Id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    cost = p.Cost,
                    image = p.Image,
                    date = p.Date,
                    productType = new { id = p.ProductType.Id, name = p.ProductType.Name },
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Producto creado exitosamente: {productName} con ID: {productId}", request.Name, newProduct.Id);
            return CreatedAtAction(nameof(GetProduct), new { id = newProduct.Id }, createdProduct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear producto");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Actualiza un producto existente
    /// </summary>
    /// <param name="id">ID del producto a actualizar</param>
    /// <param name="request">Datos actualizados del producto</param>
    /// <returns>Producto actualizado</returns>
    /// <response code="200">Producto actualizado exitosamente</response>
    /// <response code="400">Datos de entrada inválidos</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="404">Producto no encontrado</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "El nombre del producto es requerido" });
            }

            if (request.ProductTypeId <= 0)
            {
                return BadRequest(new { message = "El tipo de producto es requerido" });
            }

            if (request.BusinessId <= 0)
            {
                return BadRequest(new { message = "El negocio es requerido" });
            }

            _logger.LogInformation("Actualizando producto con ID: {id}", id);

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound(new { message = "Producto no encontrado" });
            }

            // Verificar que el tipo de producto existe
            _logger.LogInformation("Verificando existencia del tipo de producto con ID: {productTypeId} para actualización", request.ProductTypeId);
            
            var productTypeExists = await _context.ProductTypes
                .AsNoTracking()
                .AnyAsync(pt => pt.Id == request.ProductTypeId);
                
            _logger.LogInformation("Resultado de verificación de tipo de producto para actualización: {exists}", productTypeExists);
            
            if (!productTypeExists)
            {
                // Log adicional para debug en actualización
                var allProductTypes = await _context.ProductTypes
                    .AsNoTracking()
                    .Select(pt => new { pt.Id, pt.Name })
                    .ToListAsync();
                    
                _logger.LogWarning("Tipo de producto {productTypeId} no encontrado en actualización. Tipos disponibles: {@productTypes}", 
                    request.ProductTypeId, allProductTypes);
                    
                return BadRequest(new { message = "El tipo de producto especificado no existe" });
            }

            // Verificar que el negocio existe
            var businessExists = await _context.Businesses.AnyAsync(b => b.Id == request.BusinessId);
            if (!businessExists)
            {
                return BadRequest(new { message = "El negocio especificado no existe" });
            }

            // Verificar SKU único si se proporciona y es diferente al actual
            if (!string.IsNullOrEmpty(request.Sku) && request.Sku != existingProduct.Sku)
            {
                var skuExists = await _context.Products.AnyAsync(p => p.Sku == request.Sku && p.Id != id);
                if (skuExists)
                {
                    return BadRequest(new { message = "El SKU ya existe" });
                }
            }

            // Actualizar campos
            existingProduct.Name = request.Name.Trim();
            existingProduct.Sku = request.Sku?.Trim();
            existingProduct.Price = request.Price;
            existingProduct.Cost = request.Cost;
            existingProduct.Image = request.Image?.Trim();
            existingProduct.ProductTypeId = request.ProductTypeId;
            existingProduct.BusinessId = request.BusinessId;

            await _context.SaveChangesAsync();

            // Obtener el producto actualizado con sus relaciones
            var updatedProduct = await _context.Products
                .Include(p => p.ProductType)
                .Include(p => p.Business)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    cost = p.Cost,
                    image = p.Image,
                    date = p.Date,
                    productType = new { id = p.ProductType.Id, name = p.ProductType.Name },
                    business = new { id = p.Business.Id, companyName = p.Business.CompanyName }
                })
                .FirstOrDefaultAsync();

            _logger.LogInformation("Producto actualizado exitosamente: {productName} con ID: {id}", request.Name, id);
            return Ok(updatedProduct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar producto con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un producto
    /// </summary>
    /// <param name="id">ID del producto a eliminar</param>
    /// <returns>Confirmación de eliminación</returns>
    /// <response code="200">Producto eliminado exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="404">Producto no encontrado</response>
    /// <response code="409">No se puede eliminar - Producto tiene stock asociado</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        try
        {
            _logger.LogInformation("Eliminando producto con ID: {id}", id);

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                return NotFound(new { message = "Producto no encontrado" });
            }

            // Verificar si tiene stock asociado
            var hasStock = await _context.Stocks.AnyAsync(s => s.ProductId == id);
            if (hasStock)
            {
                return Conflict(new { message = "No se puede eliminar el producto porque tiene movimientos de stock asociados" });
            }

            _context.Products.Remove(existingProduct);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Producto eliminado exitosamente: {productName} con ID: {id}", existingProduct.Name, id);
            return Ok(new { message = "Producto eliminado exitosamente", productId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar producto con ID: {id}", id);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Sube una imagen para un producto
    /// </summary>
    /// <param name="file">Archivo de imagen</param>
    /// <returns>URL de la imagen subida</returns>
    /// <response code="200">Imagen subida exitosamente</response>
    /// <response code="400">Archivo inválido</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpPost("upload-image")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> UploadProductImage(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No se ha seleccionado ningún archivo" });
            }

            // Validar tipo de archivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Tipo de archivo no permitido. Use: jpg, jpeg, png, gif, webp" });
            }

            // Validar tamaño (máximo 5MB)
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(new { message = "El archivo es demasiado grande. Tamaño máximo: 5MB" });
            }

            // Crear directorio si no existe
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Generar nombre único para el archivo
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            // Guardar archivo
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Generar URL relativa
            var imageUrl = $"/uploads/products/{fileName}";

            _logger.LogInformation("Imagen subida exitosamente: {fileName}", fileName);
            return Ok(new { imageUrl, fileName, message = "Imagen subida exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al subir imagen");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina una imagen de producto del servidor
    /// </summary>
    /// <param name="fileName">Nombre del archivo a eliminar</param>
    /// <returns>Confirmación de eliminación</returns>
    /// <response code="200">Imagen eliminada exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="404">Imagen no encontrada</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpDelete("delete-image/{fileName}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public ActionResult DeleteProductImage(string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest(new { message = "Nombre de archivo requerido" });
            }

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            var filePath = Path.Combine(uploadsPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "Imagen no encontrada" });
            }

            // Verificar que el archivo esté en el directorio correcto (seguridad)
            var fullUploadsPath = Path.GetFullPath(uploadsPath);
            var fullFilePath = Path.GetFullPath(filePath);
            
            if (!fullFilePath.StartsWith(fullUploadsPath))
            {
                return BadRequest(new { message = "Ruta de archivo inválida" });
            }

            System.IO.File.Delete(filePath);

            _logger.LogInformation("Imagen eliminada exitosamente: {fileName}", fileName);
            return Ok(new { message = "Imagen eliminada exitosamente", fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar imagen: {fileName}", fileName);
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene todos los negocios disponibles
    /// </summary>
    /// <returns>Lista de negocios</returns>
    /// <response code="200">Lista de negocios obtenida exitosamente</response>
    /// <response code="401">No autorizado - Token JWT requerido</response>
    /// <response code="500">Error interno del servidor</response>
    [HttpGet("business")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<object>>> GetBusinesses()
    {
        try
        {
            _logger.LogInformation("Obteniendo negocios disponibles");

            var businesses = await _context.Businesses
                .Select(b => new { id = b.Id, name = b.CompanyName })
                .ToListAsync();

            _logger.LogInformation($"Se encontraron {businesses.Count} negocios");
            return Ok(businesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener negocios");
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }
}

/// <summary>
/// Modelo para crear un nuevo tipo de producto
/// </summary>
public class CreateProductTypeRequest
{
    /// <summary>
    /// Nombre del tipo de producto/categoría
    /// </summary>
    /// <example>Electronics</example>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Modelo para crear un nuevo producto
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// Nombre del producto
    /// </summary>
    /// <example>iPhone 15 Pro</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SKU del producto (opcional)
    /// </summary>
    /// <example>IP15P256</example>
    public string? Sku { get; set; }

    /// <summary>
    /// Precio de venta
    /// </summary>
    /// <example>999.99</example>
    public int Price { get; set; }

    /// <summary>
    /// Costo del producto
    /// </summary>
    /// <example>750.00</example>
    public int Cost { get; set; }

    /// <summary>
    /// URL de la imagen del producto (opcional)
    /// </summary>
    /// <example>https://example.com/images/product.jpg</example>
    public string? Image { get; set; }

    /// <summary>
    /// ID del tipo de producto
    /// </summary>
    /// <example>1</example>
    public int ProductTypeId { get; set; }

    /// <summary>
    /// ID del negocio
    /// </summary>
    /// <example>1</example>
    public int BusinessId { get; set; }
}

/// <summary>
/// Modelo para actualizar un producto existente
/// </summary>
public class UpdateProductRequest
{
    /// <summary>
    /// Nombre del producto
    /// </summary>
    /// <example>iPhone 15 Pro Max</example>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SKU del producto (opcional)
    /// </summary>
    /// <example>IP15PM512</example>
    public string? Sku { get; set; }

    /// <summary>
    /// Precio de venta
    /// </summary>
    /// <example>1199.99</example>
    public int Price { get; set; }

    /// <summary>
    /// Costo del producto
    /// </summary>
    /// <example>850.00</example>
    public int Cost { get; set; }

    /// <summary>
    /// URL de la imagen del producto (opcional)
    /// </summary>
    /// <example>https://example.com/images/product-updated.jpg</example>
    public string? Image { get; set; }

    /// <summary>
    /// ID del tipo de producto
    /// </summary>
    /// <example>1</example>
    public int ProductTypeId { get; set; }

    /// <summary>
    /// ID del negocio
    /// </summary>
    /// <example>1</example>
    public int BusinessId { get; set; }
}

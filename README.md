# GP Inventory API Configuration

## Quick Start
1. **Verify Installation**: Run `verify.bat` to check that everything is working
2. **Start API**: Run `start-api.bat`
3. **Run Tests**: Run `run-tests-coverage.bat` for full test suite with coverage

## Database Configuration
Before running the API, make sure to update the connection string in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=143.198.232.23;Database=gp_inventory;Uid=root;Pwd=%8W(9/SNEVpT@<<bQ!U7ed;"
  },
  "JwtSettings": {
    "SecretKey": "this-is-a-super-secret-key-that-must-be-at-least-32-characters-long-for-security",
    "Issuer": "GPInventory",
    "Audience": "GPInventory"
  }
}
```

## Available Scripts

### Production Scripts
- **`verify.bat`** - Complete system verification (recommended first run)
- **`start-api.bat`** - Start the API with error checking
- **`build-and-run.bat`** - Build and start with detailed output

### Testing Scripts
- **`run-tests-coverage.bat`** - Full test suite with HTML coverage report
- **`run-unit-tests.bat`** - Unit tests only (fast)
- **`run-integration-tests.bat`** - Integration tests only
- **`run-tests.bat`** - All tests with basic coverage

## Running the API

### Option 1: Quick Start (Recommended)
```batch
verify.bat
```

### Option 2: Manual Start
```batch
start-api.bat
```

### Option 3: Development Mode
```batch
cd src\GPInventory.Api
dotnet run --urls "http://localhost:5000"
```

The API will be available at:
- **API**: `http://localhost:5000`
- **Swagger UI**: `http://localhost:5000/swagger`

## Available Endpoints

### Authentication
- `POST /api/auth/login` - Login with email and password
- `POST /api/auth/register` - Register a new user  
- `POST /api/auth/validate-token` - Validate JWT token (requires authentication)
- `GET /api/auth/me` - Get current user information (requires authentication)

### Authentication Request Examples

**Login Request:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Register Request:**
```json
{
  "email": "user@example.com",
  "name": "John",
  "lastName": "Doe", 
  "password": "password123",
  "gender": "M",
  "birthDate": "1990-01-01T00:00:00",
  "phone": 1234567890
}
```

## Testing & Code Coverage

### Run All Tests with Coverage
```batch
run-tests-coverage.bat
```

### Test Coverage Targets
- **Line Coverage**: > 80%
- **Branch Coverage**: > 75% 
- **Method Coverage**: > 85%

### Test Structure
- **Unit Tests** (`GPInventory.Tests`): Domain, Application, Infrastructure, API layers
- **Integration Tests** (`GPInventory.IntegrationTests`): End-to-end authentication flow

## Database Schema
The API uses the provided MySQL schema with the following main tables:
- `user` - User authentication and profile information
- `business` - Business/company information  
- `user_has_business` - Many-to-many relationship between users and businesses
- `role` - User roles in businesses
- `product` - Product catalog
- `product_type` - Product categories
- `stock` - Inventory tracking
- `flow_type` - Stock movement types (entrada/salida)

## Security Features
- **JWT-based authentication** with configurable secret key
- **Password hashing with salt** using BCrypt
- **Role-based access control** (ready for implementation)
- **CORS support** for frontend integration
- **Input validation** and error handling

## Project Structure (DDD)
```
gp-inventory-api/
├── src/
│   ├── GPInventory.Api/          # Web API layer (Controllers, Middleware)
│   ├── GPInventory.Application/  # Application services and DTOs
│   ├── GPInventory.Domain/       # Domain entities and business logic
│   └── GPInventory.Infrastructure/ # Data access and external services
├── tests/
│   ├── GPInventory.Tests/        # Unit tests
│   └── GPInventory.IntegrationTests/ # Integration tests
├── GPInventory.sln               # Solution file
├── verify.bat                    # System verification script
└── *.bat                         # Helper scripts
```

## Troubleshooting

### Common Issues
1. **Build Errors**: Run `verify.bat` to diagnose
2. **Database Connection**: Update connection string in `appsettings.json`
3. **JWT Errors**: Ensure `SecretKey` is at least 32 characters
4. **Test Failures**: Check `TESTING.md` for details

### Getting Help
- Check `TROUBLESHOOTING.md` for detailed solutions
- Run `verify.bat` to identify issues
- Ensure all NuGet packages are restored

This API follows Domain Driven Design (DDD) principles with proper separation of concerns across the layers and includes comprehensive testing with code coverage.

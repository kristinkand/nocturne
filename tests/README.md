# Nocturne Testing Framework

This directory contains comprehensive testing infrastructure for the Nocturne project, including unit tests, integration tests, performance tests, and migration tool validation.

## Overview

The testing framework provides:

- **Unit Tests**: Fast, isolated component testing with mocking
- **Integration Tests**: End-to-end testing with real databases via Testcontainers
- **Performance Tests**: Benchmarking and load testing with BenchmarkDotNet and NBomber
- **Migration Tool Tests**: Comprehensive validation of PostgreSQL migration functionality
- **Test Infrastructure**: Docker containers and automated test environment management

## Directory Structure

```
tests/
├── Unit/                          # Fast unit tests with mocking
│   ├── Nocturne.API.Tests/       # API component tests
│   └── Nocturne.Tools.Migration.Tests/  # Migration tool unit tests
│       ├── Services/              # Service layer tests
│       ├── ErrorHandling/         # Error scenario tests
│       ├── TestDataGeneration/    # Automated test data generators
│       └── Infrastructure/        # Test database management
├── Integration/                   # Integration tests with real databases
│   ├── Nocturne.API.Tests/       # API integration tests
│   └── Nocturne.Tools.Migration.Integration.Tests/  # Migration integration tests
│       └── DataIntegrity/         # Data integrity validation
├── Performance/                   # Performance and load testing
│   └── Nocturne.Tools.Migration.Performance.Tests/
│       ├── PerformanceBenchmarks.cs    # BenchmarkDotNet tests
│       └── LoadAndStressTests.cs       # NBomber load tests
└── Infrastructure/                # Test environment setup
    └── Docker/                    # Docker containers for testing
        ├── docker-compose.test.yml
        ├── mongodb-init/          # MongoDB initialization
        └── postgresql-init/       # PostgreSQL initialization
```

## Migration Tool Testing Framework

### Comprehensive Test Categories

✅ **Unit Tests**: All core components with 90%+ coverage

- Configuration validation tests
- Data transformation logic tests
- Service layer tests with mocking
- Utility function tests
- Error handling tests

✅ **Integration Tests**: Real database testing

- End-to-end migration tests
- Database connection tests
- Schema creation/validation tests
- Checkpoint/resume functionality tests
- Parallel processing tests

✅ **Performance Tests**: Large dataset validation

- Large dataset migration tests (1M+ records)
- Memory usage validation
- Processing speed benchmarks
- Concurrent migration tests
- Resource utilization monitoring

✅ **Data Integrity Tests**: Comprehensive validation

- Round-trip data validation
- Record count verification
- Data type preservation tests
- Relationship integrity tests
- Complex nested data tests

✅ **Error Handling Tests**: Robust error scenarios

- Connection failure handling
- Data corruption scenarios
- Resource exhaustion testing
- Recovery mechanism validation
- Graceful degradation tests

### Test Data Scenarios

The framework supports multiple test data scenarios:

- **Normal Data**: Well-formed documents with realistic values
- **Edge Cases**: Boundary conditions and special characters
- **Large Datasets**: 1M+ records for performance testing
- **Corrupt Data**: Invalid types and malformed structures

### Quick Start

```bash
# Start test infrastructure
cd tests/Infrastructure/Docker
docker-compose -f docker-compose.test.yml up -d

# Run all tests
dotnet test

# Run specific test categories
dotnet test tests/Unit/                    # Unit tests only
dotnet test tests/Integration/             # Integration tests only
dotnet test tests/Performance/             # Performance tests

# Run migration tool tests specifically
dotnet test tests/Unit/Nocturne.Tools.Migration.Tests/
dotnet test tests/Integration/Nocturne.Tools.Migration.Integration.Tests/

# Run performance benchmarks
dotnet run --project tests/Performance/Nocturne.Tools.Migration.Performance.Tests/
```

## API Testing (Legacy)

### Test Categories

#### Unit Tests

Unit tests run without external dependencies and test individual components in isolation. These tests are fast and can be run without any setup.

#### Integration Tests

Integration tests use real MongoDB containers via Testcontainers to test end-to-end functionality. These tests require Docker to be installed and running.

### Running API Tests

```bash
# Running All Tests
dotnet test

# Running Only Unit Tests
dotnet test --filter "FullyQualifiedName!~Integration"

# Running Only Integration Tests
dotnet test --filter "FullyQualifiedName~Integration"
```

### Docker Setup for Integration Tests

Integration tests require Docker to be installed and running to create MongoDB test containers.

#### Installing Docker

**Windows**

1. Download and install [Docker Desktop for Windows](https://docs.docker.com/desktop/windows/install/)
2. Start Docker Desktop
3. Ensure WSL 2 backend is enabled (recommended)

**macOS**

1. Download and install [Docker Desktop for Mac](https://docs.docker.com/desktop/mac/install/)
2. Start Docker Desktop

**Linux**

1. Install Docker Engine using your distribution's package manager
2. Start the Docker service
3. Add your user to the docker group (optional, to run without sudo)

#### Verifying Docker Installation

```bash
docker --version
docker run hello-world
```

### API Test Structure

```
Nocturne.API.Tests/
├── Controllers/           # Controller unit tests
├── Services/             # Service unit tests
├── Models/               # Model unit tests
├── Integration/          # Integration tests
│   ├── EntriesIntegrationTests.cs      # End-to-end API tests
│   └── MongoDbServiceIntegrationTests.cs # Database integration tests
└── GlobalUsings.cs       # Global using statements
```

## Performance Testing

### Available Benchmarks

1. **Data Transformation Benchmarks**

   - Single document transformation
   - Batch processing performance
   - Memory usage patterns
   - Concurrent transformation

2. **Validation Benchmarks**

   - Connection string validation
   - JSON structure validation
   - Type compatibility checking
   - Document batch validation

3. **Load Testing**
   - Sustained load simulation (100-500 ops/sec)
   - Stress testing to breaking point
   - Memory pressure scenarios
   - Concurrent migration simulation

### Running Performance Tests

# Test Execution Scripts

## Fast Tests (Excludes slow tests by default)

```bash
# Run all unit tests (excludes Performance, Integration, Load, Stress tests)
dotnet test --filter "Category!=Performance&Category!=Integration&Category!=Load&Category!=Stress&Category!=BenchmarkDotNet"

# Run unit tests with coverage
dotnet test --filter "Category!=Performance&Category!=Integration&Category!=Load&Category!=Stress&Category!=BenchmarkDotNet" --collect:"XPlat Code Coverage"
```

## Slow Tests (Run only when needed)

### Performance Tests

```bash
# Run only performance/benchmark tests
dotnet test --filter "Category=Performance|Category=BenchmarkDotNet"

# Run cache performance tests specifically
dotnet test --filter "FullyQualifiedName~CachePerformanceBenchmarks"

# Run migration performance tests
dotnet test tests/Performance/Nocturne.Tools.Migration.Performance.Tests/
```

### Integration Tests

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run integration tests with containers (Docker required)
dotnet test --filter "Category=Integration&Category=Docker"

dotnet test tests/Integration/Nocturne.Infrastructure.Messaging.Tests/
```

### Load & Stress Tests

```bash
# Run load/stress tests (takes significant time)
dotnet test --filter "Category=Load|Category=Stress"

# Run specific NBomber load tests
dotnet run --project tests/Performance/Nocturne.Tools.Migration.Performance.Tests/ -- --load
```

## Complete Test Suite (All tests including slow ones)

```bash
# Run absolutely everything (will take a long time)
dotnet test

# Run everything with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## CI/CD Recommendations

### Pull Request Validation (Fast)

```bash
dotnet test --filter "Category!=Performance&Category!=Integration&Category!=Load&Category!=Stress&Category!=BenchmarkDotNet" --logger "trx" --results-directory TestResults
```

### Nightly/Weekly Full Test Suite

```bash
dotnet test --logger "trx" --results-directory TestResults --collect:"XPlat Code Coverage"
```

### Release Validation

```bash
# Run performance benchmarks
dotnet test --filter "Category=Performance" --logger "trx" --results-directory TestResults

# Run integration tests
dotnet test --filter "Category=Integration" --logger "trx" --results-directory TestResults
```

## Adding New Tests

### Migration Tool Tests

1. **Unit Tests**: Add to appropriate service test class in `tests/Unit/Nocturne.Tools.Migration.Tests/`
2. **Integration Tests**: Use `TestDatabaseManager` for database setup in `tests/Integration/`
3. **Performance Tests**: Add benchmarks using BenchmarkDotNet patterns
4. **Test Data**: Extend `TestDataGenerator` for new scenarios

### API Tests

1. **Unit Tests**: Create in appropriate folder (Controllers, Services, Models)
2. **Integration Tests**: Add to Integration folder with database setup
3. **Use mocking**: For external dependencies in unit tests
4. **Follow patterns**: Use existing base classes and patterns

---

This comprehensive testing framework ensures reliability, performance, and data integrity across all components of the Nocturne project.

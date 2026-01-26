# oref - OpenAPS Reference Design

This is the Rust implementation of the OpenAPS reference design algorithms, compiled to WebAssembly for use in Nocturne.

## Pre-compiled Package

By default, Nocturne downloads a pre-compiled WASM binary from GitHub Releases during `dotnet restore`. This eliminates the need for a Rust toolchain in most development environments.

## Compiling from Source

If you need to modify the oref Rust code, you can enable local compilation:

1. **Install Rust toolchain:**
   ```bash
   # Install rustup from https://rustup.rs/
   rustup target add wasm32-unknown-unknown
   ```

2. **Enable compile-from-source in appsettings.json:**
   ```json
   {
     "Parameters": {
       "Oref": {
         "CompileFromSource": true
       }
     }
   }
   ```

3. **Run Aspire:**
   ```bash
   aspire run
   ```

   The oref WASM build will execute before the API starts.

## Manual Build

To build manually without Aspire:

```bash
cargo build --lib --release --features wasm --target wasm32-unknown-unknown
```

Output: `target/wasm32-unknown-unknown/release/oref.wasm`

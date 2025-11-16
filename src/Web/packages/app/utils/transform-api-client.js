#!/usr/bin/env node

/**
 * Post-processing script to transform NSwag-generated TypeScript client to use
 * object parameters instead of inline parameters.
 *
 * This script converts method signatures like: getEntries(count?: number,
 * find?: string, token?: string): Promise<Entry[]>
 *
 * To: getEntries(params?: { count?: number, find?: string, token?: string }):
 * Promise<Entry[]>
 */

const fs = require("fs");
const path = require("path");

const CLIENT_FILE_PATH = path.join(
  __dirname,
  "../src/lib/api/generated/nocturne-api-client.ts"
);

function transformApiClient() {
  if (!fs.existsSync(CLIENT_FILE_PATH)) {
    console.error(`Error: API client file not found at ${CLIENT_FILE_PATH}`);
    process.exit(1);
  }

  console.log("Reading API client file...");
  let content = fs.readFileSync(CLIENT_FILE_PATH, "utf8");

  console.log("Transforming method signatures...");

  // Regex to match method signatures with multiple parameters
  // Matches: methodName(param1?: type1, param2?: type2, ...): ReturnType
  const methodRegex = /(\w+)\(([^)]+)\):\s*(Promise<[^>]+>|[^{;]+)/g;

  let transformedCount = 0;

  content = content.replace(
    methodRegex,
    (match, methodName, params, returnType) => {
      // Skip if there's only one parameter or no parameters
      const paramList = params.trim();
      if (!paramList || !paramList.includes(",")) {
        return match;
      }

      // Skip constructors and certain special methods
      if (methodName === "constructor" || methodName.startsWith("_")) {
        return match;
      }

      // Parse parameters
      const parsedParams = parseParameters(paramList);
      if (parsedParams.length <= 1) {
        return match;
      }

      // Check if all parameters are optional (have ?)
      const allOptional = parsedParams.every((p) => p.optional);

      // Create object parameter
      const objectParams = parsedParams
        .map((p) => `${p.name}: ${p.type}`)
        .join(", ");
      const paramsOptional = allOptional ? "?" : "";

      transformedCount++;
      console.log(`  Transforming ${methodName}(...)`);

      return `${methodName}(params${paramsOptional}: { ${objectParams} }): ${returnType}`;
    }
  );

  // Also transform the method implementations to destructure the params object
  content = transformMethodImplementations(content);

  console.log(
    `Writing transformed file... (${transformedCount} methods transformed)`
  );
  fs.writeFileSync(CLIENT_FILE_PATH, content, "utf8");

  console.log("API client transformation complete!");
}

function parseParameters(paramString) {
  const params = [];
  let depth = 0;
  let current = "";
  let inString = false;
  let stringChar = "";

  for (let i = 0; i < paramString.length; i++) {
    const char = paramString[i];

    if (!inString && (char === '"' || char === "'")) {
      inString = true;
      stringChar = char;
    } else if (inString && char === stringChar && paramString[i - 1] !== "\\") {
      inString = false;
    } else if (!inString) {
      if (char === "<" || char === "{" || char === "(") {
        depth++;
      } else if (char === ">" || char === "}" || char === ")") {
        depth--;
      } else if (char === "," && depth === 0) {
        if (current.trim()) {
          params.push(parseParameter(current.trim()));
        }
        current = "";
        continue;
      }
    }

    current += char;
  }

  if (current.trim()) {
    params.push(parseParameter(current.trim()));
  }

  return params;
}

function parseParameter(param) {
  const match = param.match(/^(\w+)(\?)?:\s*(.+)$/);
  if (match) {
    return {
      name: match[1],
      optional: !!match[2],
      type: match[3],
    };
  }

  // Fallback for simple cases
  const parts = param.split(":");
  if (parts.length >= 2) {
    const name = parts[0].trim();
    const optional = name.endsWith("?");
    return {
      name: optional ? name.slice(0, -1) : name,
      optional,
      type: parts.slice(1).join(":").trim(),
    };
  }

  return { name: param, optional: false, type: "any" };
}

function transformMethodImplementations(content) {
  // Transform method implementations to destructure parameters
  // This is a more complex transformation that would need to handle the method bodies
  // For now, we'll focus on the signatures and let the developer handle the implementations

  // Look for method implementations and add destructuring
  const implRegex =
    /(\w+)\(params(\?)?:\s*\{([^}]+)\}\):\s*(Promise<[^>]+>|[^{]+)\s*\{/g;

  return content.replace(
    implRegex,
    (match, methodName, optional, paramTypes, returnType) => {
      // Parse the parameter types to get parameter names
      const paramList = paramTypes
        .split(",")
        .map((p) => {
          const match = p.trim().match(/^(\w+):/);
          return match ? match[1] : null;
        })
        .filter(Boolean);

      if (paramList.length === 0) {
        return match;
      }

      const destructuring = `{ ${paramList.join(", ")} }`;
      const defaultValue = optional ? " = {}" : "";

      return `${methodName}(${destructuring}: { ${paramTypes} }${defaultValue}): ${returnType} {`;
    }
  );
}

// Run the transformation
if (require.main === module) {
  transformApiClient();
}

module.exports = { transformApiClient };

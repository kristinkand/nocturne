#!/usr/bin/env node --experimental-strip-types

import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const API_CLIENT_PATH = path.join(
  __dirname,
  "../../Nocturne.Web/src/lib/api/generated/nocturne-api-client.ts",
);

function transformApiClient(): void {
  console.log("Adding .catch() handlers to API client...");

  if (!fs.existsSync(API_CLIENT_PATH)) {
    console.error(`API client not found at: ${API_CLIENT_PATH}`);
    process.exit(1);
  }

  let content = fs.readFileSync(API_CLIENT_PATH, "utf8");

  // First remove existing catch handlers and fix any double brackets
  content = content.replace(/\.catch\(\(\) => \{\}\);/g, '});');
  content = content.replace(/\.catch\(\(\) => \(\{\} as any\)\);/g, '});');
  content = content.replace(/\}\)\}/g, '});');

  // Add .catch() handlers based on return type
  const lines = content.split('\n');
  let currentMethod = '';
  let currentReturnType = '';

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Check if this line contains a method signature to get return type
    const methodMatch = line.match(/(\w+)\s*\([^)]*\)\s*:\s*(Promise<([^>]+)>)/);
    if (methodMatch) {
      currentMethod = methodMatch[1];
      currentReturnType = methodMatch[3]; // Extract the inner type from Promise<Type>
    }

    // Check if this line contains a return statement that needs catch
    if (line.includes('return this.http.fetch')) {
      // Find the closing of the .then() block
      let j = i;
      while (j < lines.length && !lines[j].includes('});')) {
        j++;
      }

      if (j < lines.length && lines[j].trim() === '});' && !lines[j].includes('.catch(')) {
        // Choose appropriate catch handler based on return type
        let catchHandler;
        if (currentReturnType === 'void') {
          catchHandler = '}).catch(() => {});';
        } else {
          catchHandler = '}).catch(() => ({} as any));';
        }
        
        lines[j] = lines[j].replace('});', catchHandler);
      }
    }
  }

  content = lines.join('\n');

  // Write the transformed content back to the file
  fs.writeFileSync(API_CLIENT_PATH, content, "utf8");
  console.log("API client transformation completed!");
}

// Run the transformation
transformApiClient();

export default transformApiClient;

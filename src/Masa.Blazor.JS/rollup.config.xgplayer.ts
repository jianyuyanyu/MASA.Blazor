import { defineConfig } from "rollup";
import { terser } from "rollup-plugin-terser";

import commonjs from "@rollup/plugin-commonjs";
import json from "@rollup/plugin-json";
import resolve from "@rollup/plugin-node-resolve";
import typescript from "@rollup/plugin-typescript";

export default defineConfig([
  {
    input: "./src/wrappers/xgplayer/index.ts",
    output: [
      {
        file: "../MASA.Blazor.JSComponents.Xgplayer/wwwroot/xgplayer.js",
        format: "esm",
        sourcemap: true,
      },
    ],
    plugins: [typescript(), json(), resolve(), commonjs(), terser()],
  },
]);

import js from "@eslint/js";
import { fixupPluginRules } from "@eslint/compat";
import globals from "globals";
import react from "eslint-plugin-react";
import tseslint from "typescript-eslint";
import prettier from "eslint-config-prettier/flat";

export default tseslint.config(
    { ignores: ["build/**", "node_modules/**"] },
    js.configs.recommended,
    ...tseslint.configs.recommended,
    {
        files: ["**/*.{js,ts,tsx}"],
        languageOptions: { globals: { ...globals.browser, ...globals.node } },
    },
    {
        files: ["src/**/*.{ts,tsx}"],
        plugins: { react: fixupPluginRules(react) },
        settings: { react: { version: "detect" } },
        rules: {
            ...react.configs.flat.recommended.rules,
            ...react.configs.flat["jsx-runtime"].rules,
            "react/prop-types": "off",
            "@typescript-eslint/no-unused-vars": ["error", {
                varsIgnorePattern: "^_",
                argsIgnorePattern: "^_",
                ignoreRestSiblings: true,
            }],
        },
    },
    prettier,
);

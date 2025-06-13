module.exports = {
    corePlugins: {
        preflight: false,
    },
    mode: "jit",
    content: [
        "./**/*.razor",
        "!./obj/**/*",
        "!./**/obj/**/*",
        "!./bin/**/*",
        "!./**/bin/**/*"

    ],
    theme: {
        extend: {}
    },
    plugins: [
        require("@tailwindcss/typography"),
    ]
}
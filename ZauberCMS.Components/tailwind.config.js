module.exports = {
/*  corePlugins: {
    preflight: false,
  },*/
  mode: "jit",
  content: ["**/*.razor"],
  theme: {
    extend: {}
  },
  plugins: [
    require("@tailwindcss/typography"),
  ]
}
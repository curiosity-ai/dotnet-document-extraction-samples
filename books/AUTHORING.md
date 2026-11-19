# Authoring the sample books

The three books in `books/src/` are short, beginner-friendly introductions written for this
repository. They exist to give document extraction providers realistic input: headings,
running headers and footers, tables (including a merged header cell), figures, code, and
callouts. They are original condensed rewrites of Luis Quintanilla's longer guides.

## Layout

```text
books/src/<slug>/
  book.md           the book (front matter + Markdown)
  figures/*.svg     figures referenced from book.md
```

## `book.md` format

Front matter (simple `key: value` lines between `---` fences):

```text
---
title: Tokens, Explained
subtitle: A short introduction to tokenization in .NET
header: Tokens, Explained
author: Luis Quintanilla
---
```

Body rules (the renderer is Markdig with advanced extensions, HTML allowed, US Letter page):

- Start with `# Title` on the title page, then a short "In this book" list, then
  `<div class="page-break"></div>`.
- Use `##` for chapters and `###` for subsections. Do not skip levels.
- Tables that need merged cells use raw HTML `<table>` with `colspan`. Other tables use
  Markdown pipe tables.
- Figures: `![Alt text](figures/name.svg)` on its own line, immediately followed by a
  caption line `<p class="caption">Figure N. Caption text.</p>`. The build rasterizes SVGs
  to PNG so the PDF contains real images.
- Callouts: `<div class="callout"><strong>Tip.</strong> text</div>`.
- Code: fenced blocks with a language (`csharp`, `text`, `console`). Keep each code block
  under 20 lines and lines under 80 characters.
- Page breaks: `<div class="page-break"></div>`. Target 4 to 6 pages total.
- No em dashes. Plain, friendly, second-person teaching voice. Short paragraphs.
- Every technical claim and every API name must be correct for current .NET
  (`Microsoft.ML.Tokenizers`, `System.Numerics.Tensors`, and so on). If the source guide
  shows an API, keep its usage. Do not invent APIs or numbers.

## Figures

Hand-written SVG, 800 x 450 viewBox (or 800 x 300 for wide strips), white background,
system sans-serif font (`font-family="Segoe UI, Helvetica, Arial, sans-serif"`), text at
least 18px, dark text (#1b1b1b) with one accent color (#512BD4, .NET purple) and one
secondary (#0F6CBD). Figures must contain readable text labels, because OCR and vision
models are tested on them.

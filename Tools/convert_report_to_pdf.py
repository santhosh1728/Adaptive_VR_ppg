import re
import os
import subprocess
import base64

def image_to_base64(path):
    # Strip file:/// if present
    clean_path = path.replace('file:///', '').replace('file://', '')
    if os.name == 'nt' and clean_path.startswith('/') and len(clean_path) > 2 and clean_path[2] == ':':
        clean_path = clean_path[1:]
    clean_path = os.path.normpath(clean_path)
    if os.path.exists(clean_path):
        ext = os.path.splitext(clean_path)[1].lower()
        mime = 'image/jpeg' if ext in ['.jpg', '.jpeg'] else 'image/png'
        try:
            with open(clean_path, 'rb') as f:
                b64 = base64.b64encode(f.read()).decode('utf-8')
                return f"data:{mime};base64,{b64}"
        except:
            return path
    return path

def markdown_to_html(md_text):
    lines = md_text.split('\n')
    html_lines = []
    in_code_block = False
    code_lang = ""
    code_content = []
    in_table = False
    table_rows = []
    in_list = False

    for line in lines:
        stripped = line.strip()

        # Code block handling
        if stripped.startswith('```'):
            if in_code_block:
                in_code_block = False
                full_code = '\n'.join(code_content)
                if code_lang == 'mermaid':
                    html_lines.append(f'<div class="mermaid-diagram"><pre>{full_code}</pre></div>')
                else:
                    html_lines.append(f'<pre><code class="language-{code_lang}">{full_code}</code></pre>')
                code_content = []
                code_lang = ""
            else:
                in_code_block = True
                code_lang = stripped[3:].strip()
            continue

        if in_code_block:
            code_content.append(line.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;'))
            continue

        # Table handling
        if '|' in stripped and ('|' in line[:5] or line.startswith('|')):
            # Check if separator row
            if re.match(r'^\s*\|?\s*[-:]+[-| :]*\|?\s*$', stripped):
                continue
            cols = [c.strip() for c in stripped.strip('|').split('|')]
            if not in_table:
                in_table = True
                table_rows = []
                html_lines.append('<table>')
                html_lines.append('<thead><tr>' + ''.join(f'<th>{c}</th>' for c in cols) + '</tr></thead>')
                html_lines.append('<tbody>')
            else:
                html_lines.append('<tr>' + ''.join(f'<td>{c}</td>' for c in cols) + '</tr>')
            continue
        else:
            if in_table:
                html_lines.append('</tbody></table>')
                in_table = False

        # Headings
        if stripped.startswith('# '):
            html_lines.append(f'<h1>{stripped[2:]}</h1>')
            continue
        elif stripped.startswith('## '):
            html_lines.append(f'<h2>{stripped[3:]}</h2>')
            continue
        elif stripped.startswith('### '):
            html_lines.append(f'<h3>{stripped[4:]}</h3>')
            continue
        elif stripped.startswith('#### '):
            html_lines.append(f'<h4>{stripped[5:]}</h4>')
            continue

        # Horizontal Rule
        if stripped in ['---', '***', '___']:
            html_lines.append('<hr/>')
            continue

        # Blockquote
        if stripped.startswith('> '):
            quote_text = stripped[2:]
            html_lines.append(f'<blockquote>{quote_text}</blockquote>')
            continue

        # Unordered list
        if stripped.startswith('* ') or stripped.startswith('- '):
            item = stripped[2:]
            html_lines.append(f'<li>{item}</li>')
            continue

        # Numbered list
        num_match = re.match(r'^\d+\.\s+(.*)$', stripped)
        if num_match:
            html_lines.append(f'<li>{num_match.group(1)}</li>')
            continue

        # Empty line
        if not stripped:
            html_lines.append('<div class="spacer"></div>')
            continue

        # Regular paragraph
        html_lines.append(f'<p>{line}</p>')

    if in_table:
        html_lines.append('</tbody></table>')

    body_html = '\n'.join(html_lines)

    # Inline replacements
    # Images: ![alt](url)
    def replace_img(match):
        alt = match.group(1)
        url = match.group(2)
        b64_url = image_to_base64(url)
        return f'<div class="figure-container"><img src="{b64_url}" alt="{alt}"/><div class="figure-caption">{alt}</div></div>'

    body_html = re.sub(r'!\[([^\]]*)\]\(([^)]+)\)', replace_img, body_html)

    # Links: [text](url) -> <strong>text</strong> or styled
    body_html = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<span class="link-text">\1</span>', body_html)

    # Bold: **text**
    body_html = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', body_html)

    # Italic: *text*
    body_html = re.sub(r'(?<!\*)\*([^*]+)\*(?!\*)', r'<em>\1</em>', body_html)

    # Inline Code: `code`
    body_html = re.sub(r'`([^`]+)`', r'<code>\1</code>', body_html)

    # KaTeX / Math inline formulas: $formula$
    body_html = re.sub(r'\$([^$]+)\$', r'<span class="math-inline">\1</span>', body_html)

    # Clean up checkbox lists
    body_html = body_html.replace('<li>[x] ', '<li class="task-done">☑ ')
    body_html = body_html.replace('<li>[ ] ', '<li class="task-todo">☐ ')

    return body_html

def build_full_html(body_content):
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>AM5011 - VR Project Report</title>
<style>
    @page {{
        size: A4 portrait;
        margin: 16mm 14mm 16mm 14mm;
        @bottom-right {{
            content: "Page " counter(page);
            font-size: 8pt;
            color: #64748b;
        }}
        @top-right {{
            content: "AM5011 Virtual Reality - Final Project Report";
            font-size: 8pt;
            color: #64748b;
        }}
    }}
    body {{
        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        font-size: 9.5pt;
        line-height: 1.45;
        color: #1e293b;
        background: #ffffff;
        margin: 0;
        padding: 0;
    }}
    h1 {{
        font-size: 17pt;
        color: #0f2b48;
        border-bottom: 2.5px solid #0f2b48;
        padding-bottom: 5px;
        margin-top: 0;
        margin-bottom: 10px;
        text-transform: uppercase;
        letter-spacing: 0.5px;
    }}
    h2 {{
        font-size: 12.5pt;
        color: #1e3a8a;
        border-bottom: 1.5px solid #cbd5e1;
        padding-bottom: 3px;
        margin-top: 16pt;
        margin-bottom: 6pt;
        page-break-after: avoid;
    }}
    h3 {{
        font-size: 10.5pt;
        color: #0369a1;
        margin-top: 11pt;
        margin-bottom: 4pt;
        page-break-after: avoid;
    }}
    h4 {{
        font-size: 9.5pt;
        color: #334155;
        margin-top: 8pt;
        margin-bottom: 3pt;
        page-break-after: avoid;
    }}
    p {{
        margin-top: 3pt;
        margin-bottom: 5pt;
        text-align: justify;
    }}
    .spacer {{
        height: 4pt;
    }}
    hr {{
        border: none;
        border-top: 1px solid #cbd5e1;
        margin: 10pt 0;
    }}
    table {{
        border-collapse: collapse;
        width: 100%;
        margin: 8pt 0;
        font-size: 8pt;
        page-break-inside: avoid;
    }}
    th {{
        background-color: #f1f5f9;
        color: #0f172a;
        font-weight: 700;
        text-align: left;
        padding: 4px 6px;
        border: 1px solid #94a3b8;
    }}
    td {{
        padding: 3.5px 6px;
        border: 1px solid #cbd5e1;
        vertical-align: top;
    }}
    tr:nth-child(even) {{
        background-color: #f8fafc;
    }}
    ul, ol {{
        margin-top: 2pt;
        margin-bottom: 5pt;
        padding-left: 18pt;
    }}
    li {{
        margin-bottom: 2pt;
    }}
    .task-done {{
        list-style-type: none;
        color: #059669;
        font-weight: 500;
    }}
    .task-todo {{
        list-style-type: none;
        color: #64748b;
    }}
    blockquote {{
        border-left: 3.5px solid #0284c7;
        background-color: #f0f9ff;
        margin: 6pt 0;
        padding: 4pt 8pt;
        font-style: italic;
        color: #0369a1;
        font-size: 9pt;
        page-break-inside: avoid;
    }}
    pre {{
        background: #f8fafc;
        border: 1px solid #cbd5e1;
        border-radius: 4px;
        padding: 6pt;
        font-size: 7.5pt;
        font-family: "Consolas", "Courier New", monospace;
        overflow-x: auto;
        white-space: pre-wrap;
        margin: 5pt 0;
        page-break-inside: avoid;
    }}
    code {{
        font-family: "Consolas", monospace;
        background: #f1f5f9;
        color: #094067;
        padding: 1px 3px;
        border-radius: 3px;
        font-size: 8.5pt;
        border: 1px solid #e2e8f0;
    }}
    .math-inline {{
        font-family: "Cambria Math", "Times New Roman", serif;
        font-style: italic;
        color: #0f172a;
        padding: 0 1px;
    }}
    .link-text {{
        color: #0284c7;
        font-weight: 500;
    }}
    .figure-container {{
        text-align: center;
        margin: 10pt auto;
        page-break-inside: avoid;
    }}
    .figure-container img {{
        max-width: 90%;
        max-height: 220px;
        border: 1px solid #94a3b8;
        border-radius: 4px;
        box-shadow: 0 1px 3px rgba(0,0,0,0.1);
    }}
    .figure-caption {{
        font-size: 8pt;
        font-weight: 600;
        color: #475569;
        margin-top: 3pt;
    }}
    .mermaid-diagram pre {{
        background: #f0fdf4;
        border: 1px solid #86efac;
        color: #166534;
        font-weight: 600;
    }}
</style>
</head>
<body>
{body_content}
</body>
</html>
"""

def generate_pdf():
    md_path = r"C:\Users\SANTHOSH\.gemini\antigravity\brain\e2970e5c-39ee-42e6-87e8-84d937ee73c7\AM5011_VR_Final_Project_Report.md"
    html_out = r"c:\Users\SANTHOSH\My project (1)\AM5011_VR_Final_Project_Report.html"
    pdf_out = r"c:\Users\SANTHOSH\My project (1)\AM5011_VR_Final_Project_Report.pdf"

    print("Reading Markdown...")
    with open(md_path, 'r', encoding='utf-8') as f:
        md_text = f.read()

    print("Converting to HTML with embedded base64 media...")
    body = markdown_to_html(md_text)
    full_html = build_full_html(body)

    with open(html_out, 'w', encoding='utf-8') as f:
        f.write(full_html)
    print(f"HTML generated at: {html_out}")

    print("Invoking Microsoft Edge headless print-to-pdf...")
    edge_exe = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
    cmd = [
        edge_exe,
        "--headless",
        "--disable-gpu",
        "--no-sandbox",
        "--no-pdf-header-footer",
        f"--print-to-pdf={pdf_out}",
        f"file:///{html_out}"
    ]

    res = subprocess.run(cmd, capture_output=True, text=True)
    if os.path.exists(pdf_out):
        size_kb = os.path.getsize(pdf_out) / 1024
        print(f"SUCCESS! PDF successfully created: {pdf_out} ({size_kb:.1f} KB)")
    else:
        print(f"Failed. Returncode: {res.returncode}")
        print("Stdout:", res.stdout)
        print("Stderr:", res.stderr)

if __name__ == "__main__":
    generate_pdf()

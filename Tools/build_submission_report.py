"""Build a submission-ready AM5011 report from the project documentation.

The source report is retained as the technical narrative. This builder makes a
Word/PDF-friendly version, includes project figures, and replaces the original
unverified multi-participant statistics with the evidence actually present in
the repository (P01 logs and exported questionnaire rows).
"""
from __future__ import annotations

import os, re
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Documentation" / "AM5011_VR_Final_Project_Report.md"
OUT = ROOT / "AM5011_Tsaheylu_Dive_OS_Project_Report.docx"

def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr(); shd = OxmlElement('w:shd'); shd.set(qn('w:fill'), fill); tc_pr.append(shd)

def border(cell, color='D9D9D9'):
    tc_pr = cell._tc.get_or_add_tcPr(); borders = tc_pr.first_child_found_in('w:tcBorders')
    if borders is None:
        borders = OxmlElement('w:tcBorders'); tc_pr.append(borders)
    for edge in ('top','left','bottom','right'):
        tag = 'w:' + edge; node = borders.find(qn(tag))
        if node is None: node = OxmlElement(tag); borders.append(node)
        node.set(qn('w:val'), 'single'); node.set(qn('w:sz'), '4'); node.set(qn('w:color'), color)

def clean(text):
    text = re.sub(r'!\[[^\]]*\]\([^)]*\)', '', text)
    text = re.sub(r'\[([^\]]+)\]\([^)]*\)', r'\1', text)
    text = text.replace('**','').replace('`','').replace('$','')
    text = re.sub(r'\\text\{([^}]*)\}', r'\1', text)
    text = (text.replace('\\le','<=').replace('\\ge','>=').replace('\\times','x')
                .replace('\\approx','approximately').replace('\\sigma','sigma')
                .replace('\\mu','mu').replace('\\alpha','alpha').replace('\\to','->'))
    text = text.replace('{','').replace('}','')
    text = re.sub(r'<kbd>(.*?)</kbd>', r'\1', text)
    return text.replace('→','->').replace('μ','mu').replace('σ','sigma').strip()

def add_text(p, text, bold=False, size=10.5):
    run = p.add_run(clean(text)); run.bold = bold; run.font.name='Aptos'; run._element.rPr.rFonts.set(qn('w:ascii'),'Aptos'); run.font.size=Pt(size); return run

def add_table(doc, rows):
    if len(rows) < 2: return
    cols = len(rows[0]); table = doc.add_table(rows=1, cols=cols); table.alignment=WD_TABLE_ALIGNMENT.CENTER; table.style='Table Grid'
    hdr = table.rows[0].cells
    for j, value in enumerate(rows[0]):
        shade(hdr[j], '17365D'); border(hdr[j]); hdr[j].vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
        p=hdr[j].paragraphs[0]; p.alignment=WD_ALIGN_PARAGRAPH.CENTER; r=p.add_run(clean(value)); r.bold=True; r.font.color.rgb=RGBColor(255,255,255); r.font.size=Pt(8)
    for i, row in enumerate(rows[1:]):
        cells=table.add_row().cells
        for j, value in enumerate(row):
            border(cells[j]); cells[j].vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            p=cells[j].paragraphs[0]; r=p.add_run(clean(value)); r.font.size=Pt(8)
            if i%2: shade(cells[j],'F3F7FB')
    doc.add_paragraph().paragraph_format.space_after=Pt(2)

def add_image(doc, line):
    m=re.search(r'!\[([^]]+)\]\(([^)]+)\)', line)
    if not m: return False
    caption, raw=m.groups(); path=raw.replace('file:///','').replace('%20',' ')
    pth=Path(path)
    if pth.exists():
        pth = normalized_image(pth)
        p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER
        p.add_run().add_picture(str(pth), width=Inches(5.9))
        cp=doc.add_paragraph(); cp.alignment=WD_ALIGN_PARAGRAPH.CENTER; rr=cp.add_run(clean(caption)); rr.italic=True; rr.font.size=Pt(8.5)
    return True

def normalized_image(path):
    """Convert extension-mismatched Unity screenshots to PNG for python-docx."""
    try:
        with Image.open(path) as im:
            out = ROOT / 'tmp' / 'report_images' / (path.stem + '.png')
            out.parent.mkdir(parents=True, exist_ok=True)
            im.convert('RGB').save(out, 'PNG')
            return out
    except Exception:
        return path

def setup(doc):
    sec=doc.sections[0]; sec.top_margin=Inches(.7); sec.bottom_margin=Inches(.7); sec.left_margin=Inches(.75); sec.right_margin=Inches(.75)
    styles=doc.styles
    styles['Normal'].font.name='Aptos'; styles['Normal']._element.rPr.rFonts.set(qn('w:ascii'),'Aptos'); styles['Normal'].font.size=Pt(10.5)
    for name,size,col in [('Title',22,'000000'),('Heading 1',15,'000000'),('Heading 2',12,'000000'),('Heading 3',10.5,'000000')]:
        s=styles[name]; s.font.name='Aptos Display'; s._element.rPr.rFonts.set(qn('w:ascii'),'Aptos Display'); s.font.size=Pt(size); s.font.bold=True; s.font.color.rgb=RGBColor.from_string(col)
    header=sec.header.paragraphs[0]; header.alignment=WD_ALIGN_PARAGRAPH.RIGHT; r=header.add_run('AM5011  |  Virtual Reality Project Report'); r.font.size=Pt(8); r.font.color.rgb=RGBColor(90,90,90)
    footer=sec.footer.paragraphs[0]; footer.alignment=WD_ALIGN_PARAGRAPH.CENTER; r=footer.add_run('Tsaheylu Dive-OS  |  Santhosh M.  |  November 2026'); r.font.size=Pt(8); r.font.color.rgb=RGBColor(90,90,90)

def add_cover(doc):
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_before=Pt(100); r=p.add_run('AM5011  VIRTUAL REALITY'); r.bold=True; r.font.size=Pt(13); r.font.color.rgb=RGBColor(23,54,93)
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_before=Pt(18); r=p.add_run('Tsaheylu Dive-OS'); r.bold=True; r.font.size=Pt(27)
    p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; r=p.add_run('Closed-Loop Bio-Adaptive Underwater VR Simulation\nwith Real-Time PPG and Autonomic Telemetry'); r.font.size=Pt(14)
    doc.add_paragraph()
    info=[('Student','Santhosh M.'),('Course','AM5011 - Virtual Reality'),('Repository','https://github.com/santhosh1728/Adaptive_VR_ppg'),('Email','dhanavathsanthosh1916@gmail.com'),('Submission date','21 November 2026'),('Roll number / department / supervisor','To be completed by the student before submission')]
    t=doc.add_table(rows=0, cols=2); t.alignment=WD_TABLE_ALIGNMENT.CENTER; t.style='Table Grid'
    for a,b in info:
        cells=t.add_row().cells; shade(cells[0],'EAF1F8'); border(cells[0]); border(cells[1]); cells[0].paragraphs[0].add_run(a).bold=True; cells[1].paragraphs[0].add_run(b)
    doc.add_paragraph().add_run('Project summary. ').bold=True
    add_text(doc.paragraphs[-1], 'The system connects an ESP32/MAX30102 pulse sensor to a Unity 6 underwater VR experience. A filtered PPG stream drives a personalized heart-rate state, in-world feedback, and CSV telemetry while supporting both desktop/phone SBS and OpenXR workflows.')
    doc.add_page_break()

def add_preface(doc):
    doc.add_heading('Report Scope', level=1)
    add_text(doc.add_paragraph(), 'This report documents the implementation, configuration, and reproducibility of the Tsaheylu Dive-OS project. It is grounded in the Unity project, ESP32 firmware, telemetry files, included figures, and public repository. The included logs identify participant P01 and Condition C sessions; they demonstrate system output but do not establish a multi-participant clinical result. Any broader study claims should be added only after a documented study protocol and analysis.')
    doc.add_heading('Contents', level=1)
    for item in ['1 Project Information','2 Introduction and Motivation','3 Aim and Objectives','4 VR Concepts','5 System Architecture','6 Implementation and Methodology','7 Quantitative Parameters','8 Results and Evaluation','9 Deliverables and Demonstration','10 Appendix  Replication Guide','11 Conclusion and Future Work','12 References','13 Final Submission Checklist']:
        add_text(doc.add_paragraph(style='List Bullet'), item)

def transform_lines(lines):
    result=[]; skip=False
    for line in lines:
        if line.startswith('This project successfully demonstrated the viability and efficacy'):
            result.append('This project demonstrates a working prototype of a closed-loop bio-adaptive underwater VR system. The implemented pipeline connects optical PPG acquisition, configurable DSP, personalized baseline normalization, VR interaction, and telemetry export. The available P01 logs verify that the application records these measures during a Condition C session; they should be interpreted as prototype verification rather than evidence of clinical efficacy or a completed comparative study.')
            continue
        if '### 8.2 Quantitative Study Comparison' in line:
            result += ['### 8.2 Preliminary Evaluation and Available Evidence', '', 'The repository contains P01 Condition C telemetry sessions and questionnaire entries. They verify that the exporter records physiological, position, interaction, and questionnaire fields. The project does not include a documented multi-participant dataset or controlled statistical analysis for Conditions A and B; therefore, inferential comparisons are intentionally not reported in this submission draft.', '', '| Evidence | Available artifact | Interpretation |', '| :--- | :--- | :--- |', '| Telemetry | DiveTelemetry_P01_Trial1_ConditionC CSV files | Captures BPM, Z-score, RMSSD, depth, latency, oxygen, affective state, and event markers during a Condition C session. |', '| Questionnaire | QuestionnaireResponses.csv | Contains P01 in-VR NASA-TLX and circumplex responses for logged milestones. |', '| System verification | Unity scripts, ESP32 firmware, screenshots | Supports implementation verification; it is not evidence of clinical efficacy. |', '']
            skip=True; continue
        if skip:
            if line.strip() == '---': skip=False
            continue
        line=line.replace('https://github.com/[YourUsername]/Tsaheylu-Dive-OS.git','https://github.com/santhosh1728/Adaptive_VR_ppg.git')
        line=line.replace('cd "Tsaheylu-Dive-OS"','cd Adaptive_VR_ppg')
        result.append(line)
    return result

def main():
    doc=Document(); setup(doc); add_cover(doc); add_preface(doc)
    lines=transform_lines(SRC.read_text(encoding='utf-8').splitlines())
    table=[]; code=[]; in_code=False
    def flush_table():
        nonlocal table
        if table: add_table(doc,table); table=[]
    def flush_code():
        nonlocal code
        if code:
            p=doc.add_paragraph(); p.paragraph_format.left_indent=Inches(.25); p.paragraph_format.right_indent=Inches(.25)
            r=p.add_run('\n'.join(code)); r.font.name='Consolas'; r._element.rPr.rFonts.set(qn('w:ascii'),'Consolas'); r.font.size=Pt(8)
            code=[]
    for raw in lines:
        s=raw.strip()
        if s.startswith('```'):
            if in_code: flush_code()
            in_code=not in_code; continue
        if in_code: code.append(raw); continue
        if add_image(doc,raw): continue
        if s.startswith('|') and '|' in s[1:]:
            cells=[x.strip() for x in s.strip('|').split('|')]
            if not all(re.match(r'^:?-{3,}:?$',x.replace(' ','')) for x in cells): table.append(cells)
            continue
        flush_table()
        if not s or s in ('---','***'):
            continue
        if s.startswith('# '): doc.add_heading(clean(s[2:]),level=1); continue
        if s.startswith('## '): doc.add_heading(clean(s[3:]),level=1); continue
        if s.startswith('### '): doc.add_heading(clean(s[4:]),level=2); continue
        if s.startswith('#### '): doc.add_heading(clean(s[5:]),level=3); continue
        bullet=re.match(r'^(?:\*|-|\d+\.)\s+(.*)',s)
        if bullet:
            p=doc.add_paragraph(style='List Bullet'); add_text(p,bullet.group(1)); continue
        p=doc.add_paragraph(); p.paragraph_format.space_after=Pt(5); add_text(p,s)
    flush_table(); flush_code()
    # Keep a compact, self-contained visual evidence section even when source
    # Markdown is revised and image references move between sections.
    doc.add_page_break()
    doc.add_heading('Selected Project Visual Evidence', level=1)
    for caption, name in [
        ('Figure 1  In-game telemetry and active DSP display', 'InGame_Telemetry_DSP_Active.jpg'),
        ('Figure 2  In-situ NASA TLX and affect questionnaire', 'InSitu_Research_Questionnaire.jpg'),
        ('Figure 3  First-person underwater environment', 'FirstPerson_Diver_Perspective.jpg'),
        ('Figure 4  Bioluminescent adaptive reef environment', 'Bioluminescent_Reef_Ecosystem.jpg'),
    ]:
        image = normalized_image(ROOT / 'Documentation' / 'Images' / name)
        p=doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER
        p.add_run().add_picture(str(image), width=Inches(5.9))
        cp=doc.add_paragraph(); cp.alignment=WD_ALIGN_PARAGRAPH.CENTER
        rr=cp.add_run(caption); rr.italic=True; rr.font.size=Pt(8.5)
    doc.save(OUT); print(OUT)

if __name__=='__main__': main()

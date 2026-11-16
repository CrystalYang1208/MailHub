"""Explicit allowlist packaging; never copies private data or cleans the source workspace."""
from pathlib import Path
import argparse,hashlib,json,re,shutil,zipfile
p=argparse.ArgumentParser();p.add_argument('--work',required=True);p.add_argument('--output',required=True);args=p.parse_args()
root=Path(__file__).resolve().parents[1];work=Path(args.work).resolve();out=Path(args.output).resolve()
if work.exists():raise SystemExit('Use a NEW staging directory')
stage=work/'staging'/'MailHub';stage.mkdir(parents=True);out.mkdir(parents=True,exist_ok=True)
rootfiles=['README.md','CONTRIBUTING.md','.editorconfig','.gitignore','.npmrc','global.json','package.json','package-lock.json','redocly.yaml','DEVELOPMENT_HANDOFF.md','MODULE_OWNERSHIP.md','TEAM_A_TASKS.md','TEAM_B_TASKS.md','UNASSIGNED_REQUIREMENTS.md','INTEGRATION_CHECKLIST.md','HANDOFF_VALIDATION_REPORT.md']
trees=['frontend','backend','database','contracts','openspec','.agents','.github','tools','docs/technical/design','docs/collaboration','docs/requirements']
extra=['docs/technical/STAGE4_ACCEPTANCE.md','docs/technical/STAGE4_ENGINEERING_REPORT.md']
blocked={'.git','.local','.npm-cache','node_modules','bin','obj','dist','coverage','TestResults','test-results','playwright-report','private','credentials','evidence','__pycache__','validation'}
blockedext={'.db','.sqlite','.sqlite3','.cipher','.eml','.msg','.png','.jpg','.jpeg','.log','.pfx','.key','.exe','.dll','.pdb','.zip','.pyc'}
def safe(path):
 rel=path.relative_to(root)
 return not (set(rel.parts)&blocked) and path.suffix.lower() not in blockedext and (not path.name.startswith('.env') or path.name=='.env.example') and not path.is_symlink()
selected=[root/f for f in rootfiles+extra]
for tree in trees:
 # Prune directories before traversal: do not inspect private caches/data.
 import os
 for base,dirs,files in os.walk(root/tree):
  dirs[:]=[d for d in dirs if d not in blocked and not (Path(base)/d).is_symlink()]
  selected += [Path(base)/f for f in files if safe(Path(base)/f)]
selected=sorted(set(selected))
findings=[]
patterns={'private-key':r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----','google-secret-value':r'GOCSPX-[A-Za-z0-9_-]{8,}','google-access-token':r'ya29\.[A-Za-z0-9_-]{15,}','google-refresh-token':r'1//[A-Za-z0-9_-]{20,}','personal-path':r'[CD]:[\\/]Users[\\/][^\s"<>]+'}
for path in selected:
 if not path.is_file() or not safe(path):raise SystemExit('Unsafe/missing required file: '+str(path.relative_to(root)))
 data=path.read_bytes();text=data.decode('utf-8-sig')
 for name,pattern in patterns.items():
  # The packager contains the scanner's literal signatures; no matched value is printed.
  if path.name!='prepare-handoff.py' and re.search(pattern,text,re.I):findings.append({'file':path.relative_to(root).as_posix(),'rule':name})
 for address in re.findall(r'[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}',text):
  if not re.search(r'@(?:[^@]*\.)?example\.(?:com|org|net|invalid)$',address,re.I) and not address.endswith('@example.invalid'):
   findings.append({'file':path.relative_to(root).as_posix(),'rule':'non-synthetic-email'})
 dest=stage/path.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
if findings:
 (work/'scan-findings.json').write_text(json.dumps(findings,indent=2),encoding='utf-8');raise SystemExit('Review redacted scan findings in staging; archive NOT created')
manifest={'scope':'Allowlisted developer sources only; historical private evidence and original Word remain with lead','files':[{'path':path.relative_to(stage).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for path in sorted(stage.rglob('*')) if path.is_file()]}
(stage/'HANDOFF_FILE_MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
archive=out/'MailHub-Developer-Starter.zip'
if archive.exists():raise SystemExit('Archive already exists; choose a new output directory')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for path in sorted(stage.rglob('*')):
  if path.is_file():z.write(path,'MailHub/'+path.relative_to(stage).as_posix())
summary={'archive':str(archive),'work':str(work),'fileCount':len(manifest['files'])+1,'bytes':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'scan':'PASS: explicit allowlist plus credential/email/personal-path signatures; no source private dirs read/copied','limitations':'Pattern scan is not a mathematical proof of absence of every possible secret; all payload is reviewed text/source'}
(out/'PACKAGE_INFO.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(summary,ensure_ascii=False))

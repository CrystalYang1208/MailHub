import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { parse } from 'yaml';
import { execFileSync } from 'node:child_process';
const read = p => readFileSync(p, 'utf8');
const fail = message => { throw new Error(message); };
const hash = p => createHash('sha256').update(readFileSync(p)).digest('hex');
for (const [a,b] of [['database/migrations/catalog/V0001__initial.sql','docs/technical/design/sql/catalog.sql'],['database/migrations/profile/V0001__initial.sql','docs/technical/design/sql/profile.sql']]) if(hash(a)!==hash(b)) fail(`Approved artifact changed: ${a}`);
// This approved local artifact has repeated anchors; retain a finite expansion cap.
const doc = parse(read('contracts/openapi.yaml'), { maxAliasCount: 10000 });
const historical = parse(read('docs/technical/design/openapi.yaml'), { maxAliasCount: 10000 });
const compatibility = structuredClone(doc);
delete compatibility.components.schemas.Draft.properties.activeSubmissionId;
if (JSON.stringify(compatibility) !== JSON.stringify(historical)) fail('Unexpected API change beyond documented optional Draft.activeSubmissionId');
if (!doc.openapi.startsWith('3.1.')) fail('OpenAPI 3.1 required');
const operations=[];
for(const [path,entry] of Object.entries(doc.paths)) for(const [method,op] of Object.entries(entry)) if(['get','post','put','patch','delete','options','head'].includes(method)) operations.push({path,method,id:op.operationId});
const ids = operations.map(x=>x.id);
if(ids.some(x=>!x)||new Set(ids).size!==ids.length) fail('Missing/duplicate operationId');
function refs(value) { if(!value||typeof value!=='object')return; for(const [key,item] of Object.entries(value)){if(key==='$ref'){if(!item.startsWith('#/'))fail('External reference not supported');let target=doc;for(const part of item.slice(2).split('/'))target=target?.[part.replaceAll('~1','/').replaceAll('~0','~')];if(!target)fail(`Unresolved reference ${item}`);}else refs(item);} } refs(doc);
const baseline=JSON.parse(read('docs/technical/design/baseline-input.json'));
const trace=read('docs/collaboration/REQUIREMENT_SPEC_TRACE.md');
for(const [fr] of baseline.fr)if(!trace.includes(`| ${fr} |`))fail(`Missing FR trace ${fr}`);
if(baseline.fr.length!==35||baseline.fr.filter(x=>x[2]==='Must').length!==24)fail('Baseline priority/count drift');
const manifest=JSON.parse(read('backend/src/MailHub.Host/implemented-operations.json'));
for(const id of manifest.implementedProductOperations)if(!ids.includes(id))fail(`Unknown implemented operation ${id}`);
// Re-generate to a temporary path and compare, rather than silently replacing stale types.
mkdirSync('.local/stage4',{recursive:true});
execFileSync(process.execPath,['node_modules/openapi-typescript/bin/cli.js','contracts/openapi.yaml','-o','.local/stage4/generated.d.ts'],{stdio:'pipe'});
if(read('.local/stage4/generated.d.ts').replaceAll('\r\n','\n')!==read('frontend/src/api/generated.d.ts').replaceAll('\r\n','\n')) fail('Generated types stale: npm run contracts:types');
let preserved=0;
if(existsSync('.local/stage4/preexisting-files.json'))for(const item of JSON.parse(read('.local/stage4/preexisting-files.json')).files){const path='../'+item.path;if(!existsSync(path)||hash(path)!==item.sha256)fail(`Historical artifact changed: ${item.path}`);preserved++;}
const result={utc:new Date().toISOString(),status:'PASS',operations:ids.length,schemas:Object.keys(doc.components.schemas).length,fr:baseline.fr.length,must:24,should:11,preserved,implementedProductOperations:manifest.implementedProductOperations.length,scope:'Approved artifact hashes, refs, operation IDs, FR trace, generated TS; endpoint behavior checked by backend tests'};
writeFileSync('.local/stage4/contracts.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result));

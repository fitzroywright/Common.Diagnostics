namespace Common.Diagnostics;

public static class EngineeringDiagnosticsHtml
{
    private const string LoginTemplate = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>Engineering Diagnostics - Sign In</title>
<style>
*{box-sizing:border-box}body{margin:0;min-height:100vh;display:grid;place-items:center;background:#05050a;color:#f5f2ff;font-family:Arial,Helvetica,sans-serif}.card{width:min(500px,92vw);background:#11111b;border:1px solid #29263a;border-radius:22px;overflow:hidden;box-shadow:0 20px 70px rgba(0,0,0,.45)}.stripe{height:18px;background:#f4a261}.head{padding:28px 32px 20px;border-bottom:9px solid #9d8cff}.head h1{margin:0;text-transform:uppercase;font-size:24px}.head p{margin:8px 0 0;color:#bbb6ce}.body{padding:30px 32px}.body h2{margin:0 0 10px;font-size:18px;text-transform:uppercase}.body p{line-height:1.55;color:#c9c4d8}.signin{display:block;margin-top:24px;padding:14px 18px;border-radius:24px 5px 5px 24px;background:#7dc4ff;color:#101018;text-decoration:none;text-align:center;font-weight:800;text-transform:uppercase}.note{margin-top:18px;color:#928da5;font-size:12px}.footer{height:14px;background:#ffb4a2}
</style>
</head>
<body>
<div class="card">
  <div class="stripe"></div>
  <div class="head"><h1>Engineering Diagnostics</h1><p>__APPLICATION__ Engineering Console</p></div>
  <div class="body">
    <h2>Engineer Sign In</h2>
    <p>Use your __ACCOUNT__ account. Access is controlled by the Engineering Diagnostics authorization policy and diagnostic activity is auditable.</p>
    <a class="signin" href="__SIGNIN_PATH__">Sign in</a>
    <div class="note">Level 2 and Level 1 operations remain controlled intervention gates after authentication.</div>
  </div>
  <div class="footer"></div>
</div>
</body>
</html>
""";

    private const string DashboardTemplate = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>Engineering Diagnostics</title>
<style>
:root{--space:#05050a;--panel:#11111b;--text:#f5f2ff;--muted:#bbb6ce;--orange:#f4a261;--peach:#ffb4a2;--violet:#9d8cff;--blue:#7dc4ff;--red:#ff5d73;--green:#78e08f;--yellow:#ffd166}*{box-sizing:border-box}body{margin:0;background:var(--space);color:var(--text);font-family:Arial,Helvetica,sans-serif;letter-spacing:.03em}.shell{max-width:1500px;margin:auto;padding:18px}.top{display:grid;grid-template-columns:220px 1fr 180px;gap:10px}.pill{border-radius:30px 0 0 30px;background:var(--orange);min-height:70px}.title{background:var(--panel);border-bottom:10px solid var(--violet);padding:12px 22px;text-transform:uppercase}.title h1{font-size:24px;margin:0}.title p{margin:6px 0 0;color:var(--muted)}.cap{border-radius:0 30px 30px 0;background:var(--blue)}.grid{display:grid;grid-template-columns:260px 1fr;gap:14px;margin-top:16px}.levels{display:flex;flex-direction:column;gap:9px}.level{border:0;border-radius:28px 4px 4px 28px;padding:15px 16px;text-align:left;font-weight:800;cursor:pointer;color:#111;font-size:15px}.l5{background:var(--blue)}.l4{background:var(--violet)}.l3{background:var(--green)}.l2{background:var(--yellow)}.l1{background:var(--red)}.level small{display:block;font-weight:500;margin-top:3px}.main{background:var(--panel);border:1px solid #29263a;padding:18px;min-height:570px}.statusbar{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px}.chip{padding:7px 12px;border-radius:18px;background:#2b2940}.run{display:grid;grid-template-columns:160px 1fr 170px;gap:12px;border-top:1px solid #343047;padding:12px 0}.passed{color:var(--green)}.warning{color:var(--yellow)}.failed{color:var(--red)}.interventionrequired{color:var(--orange)}.check{padding:9px 10px;margin:7px 0;background:#191724;border-left:8px solid var(--blue)}.check.failed{border-color:var(--red)}.check.warning{border-color:var(--yellow)}.check.interventionrequired{border-color:var(--orange)}textarea{width:100%;min-height:70px;background:#0b0a12;color:var(--text);border:1px solid #4a4565;padding:10px;margin:12px 0}.footer{margin-top:16px;border-top:10px solid var(--peach);padding-top:10px;color:var(--muted);font-size:12px}.busy{opacity:.55;pointer-events:none}@media(max-width:800px){.top{grid-template-columns:50px 1fr 50px}.grid{grid-template-columns:1fr}.levels{display:grid;grid-template-columns:1fr 1fr}.run{grid-template-columns:1fr}}
</style>
</head>
<body>
<div class="shell">
  <div class="top"><div class="pill"></div><div class="title"><h1>Engineering Diagnostics</h1><p>__APPLICATION__ • Engineering Console</p></div><div class="cap"></div></div>
  <div class="grid">
    <aside class="levels">
      <button class="level l5" data-level="5">LEVEL 5 — SCAN<small>Baseline health and connectivity</small></button>
      <button class="level l4" data-level="4">LEVEL 4 — ANALYSIS<small>Deeper integration and queue analysis</small></button>
      <button class="level l3" data-level="3">LEVEL 3 — VERIFICATION<small>Verify operational state and evidence</small></button>
      <button class="level l2" data-level="2">LEVEL 2 — REPAIR<small>Evidence + controlled repair gate</small></button>
      <button class="level l1" data-level="1">LEVEL 1 — CRITICAL<small>Critical intervention gate</small></button>
    </aside>
    <main class="main">
      <div class="statusbar"><span class="chip">SYSTEM: __SYSTEM__</span><span class="chip" id="consoleStatus">CONSOLE READY</span><span class="chip" id="clock"></span></div>
      <textarea id="reason" placeholder="Engineering reason / incident context (required for Level 2 and Level 1)"></textarea>
      <div id="runs">Loading diagnostic history...</div>
    </main>
  </div>
  <div class="footer">ENGINEERING DIAGNOSTICS • Diagnostic runs are auditable. Level 2/1 do not perform destructive actions automatically.</div>
</div>
<script>
const api='__API__';
const statusEl=document.getElementById('consoleStatus');
const runsEl=document.getElementById('runs');
const reasonEl=document.getElementById('reason');
setInterval(()=>document.getElementById('clock').textContent=new Date().toLocaleString(),1000);
function esc(v){return String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));}
function statusName(v){return typeof v==='number'?({1:'Passed',2:'Warning',3:'Failed',4:'InterventionRequired'}[v]||v):v;}
function levelName(v){return typeof v==='number'?({5:'Level 5 — Scan',4:'Level 4 — Analysis',3:'Level 3 — Verification',2:'Level 2 — Repair',1:'Level 1 — Critical Intervention'}[v]||v):v;}
function statusClass(v){return String(statusName(v)||'').toLowerCase();}
function renderRun(run){const checks=(run.checks||[]).map(c=>`<div class="check ${statusClass(c.status)}"><strong>${esc(c.name)}</strong> — <span class="${statusClass(c.status)}">${esc(statusName(c.status))}</span><div>${esc(c.summary)}</div>${c.evidence?`<small>${esc(c.evidence)}</small>`:''}</div>`).join('');const resolution=run.resolvedAt?`<div class="check"><strong>RESOLVED</strong> by ${esc(run.resolvedBy)} on ${new Date(run.resolvedAt).toLocaleString()}<div>${esc(run.resolution)}</div></div>`:`<button class="level l5" style="margin-top:10px" onclick="resolveRun('${run.runId}')">RESOLVE RUN</button>`;return `<section class="run"><div><strong>${esc(levelName(run.level))}</strong><br><small>${new Date(run.startedAt).toLocaleString()}</small></div><div><strong>Requested by ${esc(run.requestedBy)}</strong>${run.reason?`<div>${esc(run.reason)}</div>`:''}<div>${checks}${resolution}</div></div><div class="${statusClass(run.status)}"><strong>${esc(statusName(run.status))}</strong><br><small>${esc(run.runId)}</small></div></section>`;}
async function resolveRun(runId){const resolution=prompt('Resolution / engineering closure note:');if(!resolution||!resolution.trim())return;document.body.classList.add('busy');try{const r=await fetch(`${api}/runs/${runId}/resolve`,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({resolution:resolution.trim()})});if(!r.ok){alert(`Resolve failed (${r.status}): ${await r.text()}`);}else{await load();}}finally{document.body.classList.remove('busy');}}
async function load(){const r=await fetch(`${api}/runs?take=25`,{credentials:'same-origin'});if(!r.ok){runsEl.textContent=`Unable to load diagnostics (${r.status}).`;return;}const data=await r.json();runsEl.innerHTML=data.length?data.map(renderRun).join(''):'No diagnostic runs yet.';}
async function run(level){if((level===1||level===2)&&!reasonEl.value.trim()){alert('Level 2 and Level 1 require an engineering reason.');return;}document.body.classList.add('busy');statusEl.textContent=`RUNNING LEVEL ${level}`;try{const r=await fetch(`${api}/run`,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify({level,reason:reasonEl.value.trim()||null})});if(!r.ok){alert(`Diagnostic failed (${r.status}): ${await r.text()}`);}else{reasonEl.value='';await load();}}finally{statusEl.textContent='CONSOLE READY';document.body.classList.remove('busy');}}
document.querySelectorAll('[data-level]').forEach(b=>b.addEventListener('click',()=>run(Number(b.dataset.level))));
load();
</script>
</body>
</html>
""";

    public static string LoginPage(string applicationName, string accountLabel, string signInPath)
    {
        return LoginTemplate
            .Replace("__APPLICATION__", Html(applicationName), StringComparison.Ordinal)
            .Replace("__ACCOUNT__", Html(accountLabel), StringComparison.Ordinal)
            .Replace("__SIGNIN_PATH__", HtmlAttribute(signInPath), StringComparison.Ordinal);
    }

    public static string Dashboard(string applicationName, string systemLabel, string apiBasePath = "/api/engineering/diagnostics")
    {
        return DashboardTemplate
            .Replace("__APPLICATION__", Html(applicationName), StringComparison.Ordinal)
            .Replace("__SYSTEM__", Html(systemLabel).ToUpperInvariant(), StringComparison.Ordinal)
            .Replace("__API__", JavaScriptString(apiBasePath.TrimEnd('/')), StringComparison.Ordinal);
    }

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    private static string HtmlAttribute(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    private static string JavaScriptString(string value) => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
}

export const adminCss = `
:root {
	--bg-black: #000;
	--bg-dark: #111;
	--bg-panel: #222;
	--text-white: #fff;
	--accent-blue: #38B6FF;
	--accent-blue-hover: #0a30b4;
}

* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { background-color: var(--bg-black); color: var(--text-white); font-family: sans-serif; }
::-webkit-scrollbar { width: 10px; height: 10px; }
::-webkit-scrollbar-track { background: var(--bg-panel); }
::-webkit-scrollbar-thumb { background-color: var(--accent-blue); border-radius: 5px; }
.container { padding: 2rem; max-width: 1200px; margin: 0 auto; }
h1 { color: var(--accent-blue); margin-bottom: 1.5rem; }
h3 { margin-bottom: 1rem; color: var(--text-white); }
.card { background: var(--bg-panel); padding: 1.5rem; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.5); margin-bottom: 2rem; }
input { width: 100%; padding: 12px; margin-bottom: 12px; background-color: var(--bg-dark); color: var(--text-white); border: 1px solid #444; border-radius: 4px; font-size: 1rem; }
input:focus { outline: none; border-color: var(--accent-blue); }
.submit-btn { width: 100%; padding: 12px; background-color: var(--accent-blue); color: var(--bg-black); border: none; border-radius: 8px; font-weight: bold; cursor: pointer; margin-top: 10px; }
.submit-btn:hover { background-color: var(--accent-blue-hover); color: var(--text-white); }
table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
th, td { padding: 0.75rem; border: 1px solid #444; text-align: left; }
th { background: var(--bg-dark); color: var(--accent-blue); }
td input { margin-bottom: 0; padding: 8px; }
button:not(.submit-btn) { padding: 8px 12px; background: var(--bg-dark); color: var(--text-white); border: 1px solid #555; border-radius: 4px; cursor: pointer; }
button:not(.submit-btn):hover { background: #444; }
button.btn-save { background: #28a745; border-color: #28a745; color: white; }
button.btn-delete { background: #dc3545; border-color: #dc3545; color: white; }
.scroll-wrapper { position: relative; width: 100%; overflow-x: auto; }
`;
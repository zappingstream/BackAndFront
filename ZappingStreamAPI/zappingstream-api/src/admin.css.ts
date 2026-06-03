export const adminCss = `
:root {
	/* Paleta de colores principal */
	--bg-black: #000;
	--bg-dark: #111;
	--bg-panel: #222;
	--text-white: #fff;
	--accent-blue: #38B6FF;
	--accent-blue-hover: #0a30b4;
	--live-badge-color: rgba(204, 0, 0, 0.9);
	--premiere-badge-color: #4d68ff;
}

* {
	box-sizing: border-box;
	margin: 0;
	padding: 0;
}

html, body {
	margin: 0;
	padding: 0;
	background-color: var(--bg-black);
	color: var(--text-white);
	font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
	user-select: none;
	overscroll-behavior: none; /* Bloquea los tirones y el refresco nativo en la PWA */
	-webkit-tap-highlight-color: transparent;
}

/* Scrollbar global */
::-webkit-scrollbar {
	width: 10px;
	height: 10px;
}
::-webkit-scrollbar-track {
	background: var(--bg-panel);
}
::-webkit-scrollbar-thumb {
	background-color: var(--accent-blue);
	border-radius: 5px;
}

/* --- Contenedores y tipografía adaptados --- */
.container { padding: 2rem; max-width: 1200px; margin: 0 auto; }
h1 { color: var(--accent-blue); margin-bottom: 1.5rem; }
h3 { margin-bottom: 1rem; color: var(--text-white); }

.card {
	background: var(--bg-panel);
	padding: 1.5rem;
	border-radius: 8px;
	box-shadow: 0 4px 6px rgba(0,0,0,0.5);
	margin-bottom: 2rem;
}

/* --- Inputs adaptados al modo oscuro --- */
input {
	width: 100%;
	padding: 12px;
	margin-bottom: 12px;
	background-color: var(--bg-dark);
	color: var(--text-white);
	border: 1px solid #444;
	border-radius: 4px;
	font-size: 1rem;
}
input:focus { outline: none; border-color: var(--accent-blue); }

/* --- Botón Principal --- */
.submit-btn {
	width: 100%;
	padding: 12px;
	background-color: var(--accent-blue);
	color: var(--bg-black);
	border: none;
	border-radius: 8px;
	font-weight: bold;
	font-size: 1rem;
	cursor: pointer;
	transition: background-color 0.3s, transform 0.1s;
	margin-top: 10px;
}
.submit-btn:hover:not(:disabled) {
	background-color: var(--accent-blue-hover);
	color: var(--text-white);
}

/* --- Tablas adaptadas --- */
table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
th, td { padding: 0.75rem; border: 1px solid #444; text-align: left; }
th { background: var(--bg-dark); color: var(--accent-blue); }
td input { margin-bottom: 0; padding: 8px; }

/* --- Botones secundarios --- */
button:not(.submit-btn) {
	padding: 8px 12px;
	background: var(--bg-dark);
	color: var(--text-white);
	border: 1px solid #555;
	border-radius: 4px;
	cursor: pointer;
	font-weight: bold;
	transition: all 0.2s;
}
button:not(.submit-btn):hover { background: #444; }

/* Aumento la especificidad de los botones de la tabla para que no queden negros */
button.btn-save { background: #28a745; border-color: #28a745; color: white; }
button.btn-save:hover { background: #218838; }

button.btn-cancel { background: #6c757d; border-color: #6c757d; color: white; }
button.btn-cancel:hover { background: #5a6268; }

button.btn-delete { background: #dc3545; border-color: #dc3545; color: white; }
button.btn-delete:hover { background: #c82333; }

/* --- Flechas de Scroll Horizontales --- */
.scroll-wrapper { position: relative; width: 100%; }
.scroll-arrow { position: absolute; top: 50%; transform: translateY(-50%); width: 44px; height: 44px; background-color: rgba(17, 17, 17, 0.85); color: var(--text-white); border: 2px solid var(--accent-blue); border-radius: 50%; font-size: 1.8rem; font-weight: bold; display: flex; justify-content: center; align-items: center; cursor: pointer; z-index: 50; opacity: 0; visibility: hidden; transition: all 0.2s ease; box-shadow: 0 0 15px rgba(0,0,0,0.8); padding-bottom: 4px; }
.scroll-wrapper:hover .scroll-arrow { opacity: 1; visibility: visible; }
.scroll-arrow:hover { background-color: var(--accent-blue); color: var(--bg-black); transform: translateY(-50%) scale(1.1); box-shadow: 0 0 15px var(--accent-blue); }
.scroll-arrow:active { transform: translateY(-50%) scale(0.9); }
.scroll-arrow.disabled { opacity: 0.2 !important; cursor: default; pointer-events: none; border-color: #555; color: #555; box-shadow: none; }
.left-arrow { left: 5px; }
.right-arrow { right: 5px; }
@media (hover: none) and (pointer: coarse) {
	.scroll-arrow {
		opacity: 0.7;
		visibility: visible;
		transform: translateY(-50%) scale(0.85);
	}
	.scroll-arrow:active { opacity: 1; }
}
`;
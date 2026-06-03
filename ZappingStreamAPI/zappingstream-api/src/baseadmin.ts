import { MongoClient } from "mongodb";
import { adminCss } from "./admin.css";

// Definimos el modelo de datos para la colección 'origin'
export interface OriginRecord {
	_id?: string;
	category: string;
	city: string;
	title: string;
}

// Función de ayuda para validar la autenticación básica
function checkAuth(request: Request, env: Env): boolean {
	const auth = request.headers.get("Authorization");
	if (!auth || !env.ADMIN_PASSWORD) return false;
	const expectedAuth = `Basic ${btoa(`admin:${env.ADMIN_PASSWORD}`)}`;
	
	// Mitigación contra ataques de tiempo (Timing Attacks)
	if (auth.length !== expectedAuth.length) {
		return false;
	}

	let isMatch = 0;
	for (let i = 0; i < expectedAuth.length; i++) {
		isMatch |= auth.charCodeAt(i) ^ expectedAuth.charCodeAt(i);
	}
	
	return isMatch === 0;
}

export async function handleAdminRequest(request: Request, env: Env, ctx: ExecutionContext, corsHeaders: Record<string, string>): Promise<Response | null> {
	const url = new URL(request.url);

	// Si no es una ruta de admin/origin, devolvemos null para que index.ts continúe con las rutas públicas
	if (!url.pathname.startsWith("/admin") && !url.pathname.startsWith("/api/origin")) {
		return null;
	}

	// Verificación de autenticación para todas las rutas que empiecen con /admin o /api/origin
	if (!checkAuth(request, env)) {
		return new Response("Unauthorized. Por favor, inicie sesión.", {
			status: 401,
			headers: {
				"WWW-Authenticate": 'Basic realm="Admin Area"',
				...corsHeaders,
			},
		});
	}

	// 0. Endpoint para servir la hoja de estilos externa
	if (url.pathname === "/admin/style.css") {
		return new Response(adminCss, {
			headers: { "Content-Type": "text/css; charset=UTF-8", ...corsHeaders },
		});
	}

	// 1. La pequeña página HTML
	if (url.pathname === "/admin") {
		const html = `
<!DOCTYPE html>
<html lang="es">
<head>
	<meta charset="UTF-8">
	<meta name="viewport" content="width=device-width, initial-scale=1.0">
	<title>Admin - Base de datos Origin</title>
	<link rel="stylesheet" href="/admin/style.css">
</head>
<body>
	<div class="container">
		<h1>Gestión de Base de Datos 'Origin'</h1>
		<div class="card">
			<h3 id="formTitle">Agregar Nuevo Registro</h3>
			<form id="addChannelForm">
				<input type="text" id="_id" placeholder="ID de Youtube (ej: UC-1NCwOfa1R...)" />
				<input type="text" id="title" placeholder="Título (ej: Abitare)" required />
				<input type="text" id="category" placeholder="Categoría (ej: Stream)" required />
				<input type="text" id="city" placeholder="Ciudad (ej: Rosario)" required />
				<button type="submit" id="submitBtn" class="submit-btn">Guardar Registro</button>
			</form>
		</div>
		<div class="card">
			<h3>Registros Actuales</h3>
			<button onclick="loadChannels()" style="margin-bottom: 15px;">Refrescar Lista</button>
			<div class="scroll-wrapper" style="overflow-x: auto;">
				<table id="channelsTable">
					<thead><tr><th>ID</th><th>Título</th><th>Categoría</th><th>Ciudad</th><th>Acciones</th></tr></thead>
					<tbody></tbody>
				</table>
			</div>
		</div>
	</div>
	<script>
		let currentChannels = [];

		async function loadChannels() {
			const res = await fetch('/api/origin/channels');
			currentChannels = await res.json();
			const tbody = document.querySelector('#channelsTable tbody');
			tbody.innerHTML = currentChannels.map(c => \`<tr id="row-\${c._id}">
				<td>\${c._id || ''}</td>
				<td class="col-title">\${c.title || ''}</td>
				<td class="col-category">\${c.category || ''}</td>
				<td class="col-city">\${c.city || ''}</td>
				<td class="col-actions">
					<button onclick="editChannelInline('\${c._id}')">Editar</button>
					<button class="btn-delete" onclick="deleteChannel('\${c._id}')">Eliminar</button>
				</td>
			</tr>\`).join('');
		}

		function editChannelInline(id) {
			const channel = currentChannels.find(c => c._id === id);
			if (!channel) return;
			
			const row = document.getElementById(\`row-\${id}\`);
			row.querySelector('.col-title').innerHTML = \`<input type="text" id="edit-title-\${id}" value="\${channel.title || ''}" />\`;
			row.querySelector('.col-category').innerHTML = \`<input type="text" id="edit-category-\${id}" value="\${channel.category || ''}" />\`;
			row.querySelector('.col-city').innerHTML = \`<input type="text" id="edit-city-\${id}" value="\${channel.city || ''}" />\`;
			
			row.querySelector('.col-actions').innerHTML = \`
				<button class="btn-save" onclick="saveChannelInline('\${id}')">Guardar</button>
				<button class="btn-cancel" onclick="loadChannels()">Cancelar</button>
			\`;
		}

		async function saveChannelInline(id) {
			const title = document.getElementById(\`edit-title-\${id}\`).value;
			const category = document.getElementById(\`edit-category-\${id}\`).value;
			const city = document.getElementById(\`edit-city-\${id}\`).value;
			
			await fetch('/api/origin/channels', {
				method: 'PUT',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ _id: id, title, category, city })
			});
			loadChannels();
		}

		async function deleteChannel(id) {
			if (!confirm('¿Estás seguro de que deseas eliminar este registro?')) return;
			await fetch('/api/origin/channels', {
				method: 'DELETE',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ _id: id })
			});
			loadChannels();
		}

		document.getElementById('addChannelForm').addEventListener('submit', async (e) => {
			e.preventDefault();
			const _id = document.getElementById('_id').value;
			const title = document.getElementById('title').value;
			const category = document.getElementById('category').value;
			const city = document.getElementById('city').value;
			
			const payload = { title, category, city };
			if (_id) payload._id = _id; 

			await fetch('/api/origin/channels', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(payload)
			});
			e.target.reset();
			loadChannels();
		});
		// Carga inicial
		loadChannels();
	</script>
</body>
</html>`;
		return new Response(html, {
			headers: { "Content-Type": "text/html; charset=UTF-8" },
		});
	}

	// 2. Endpoints para interactuar con MongoDB (Base: origin)
	if (url.pathname === "/api/origin/channels") {
		const client = new MongoClient(env.MONGODB_URI);
		try {
			await client.connect();
			const db = client.db("zappingstreamdb");
			const collection = db.collection<OriginRecord>("origin");

			if (request.method === "GET") {
				const channels = await collection.find({}).toArray();
				return Response.json(channels, { headers: corsHeaders });
			}

			if (request.method === "POST") {
				const body = await request.json() as OriginRecord;
				const result = await collection.insertOne(body);
				return Response.json({ success: true, insertedId: result.insertedId }, { status: 201, headers: corsHeaders });
			}

			if (request.method === "PUT") {
				const body = await request.json() as OriginRecord;
				if (!body._id) {
					return Response.json({ error: "Falta el _id para actualizar" }, { status: 400, headers: corsHeaders });
				}
				const { _id, ...updateFields } = body;

				const result = await collection.updateOne({ _id }, { $set: updateFields });
				return Response.json({ success: true, modifiedCount: result.modifiedCount }, { status: 200, headers: corsHeaders });
			}

			if (request.method === "DELETE") {
				const body = await request.json() as { _id: string };
				if (!body._id) {
					return Response.json({ error: "Falta el _id para eliminar" }, { status: 400, headers: corsHeaders });
				}
				const result = await collection.deleteOne({ _id: body._id });
				return Response.json({ success: true, deletedCount: result.deletedCount }, { status: 200, headers: corsHeaders });
			}
		} catch (error: any) {
			return Response.json({ error: error.message }, { status: 500, headers: corsHeaders });
		} finally {
			ctx.waitUntil(client.close());
		}
	}

	// Si llegó aquí (por ej: es /admin/algoExtra y no lo manejamos) devolvemos 404
	return new Response("Not found", { status: 404, headers: corsHeaders });
}
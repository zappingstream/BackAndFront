import { getMongoClient } from "./mongodb.js";
import { adminCss } from "./admin.css.js";

function normalizeName(name) {
    if (!name) return "";
    return name.trim().replace(/\s+/g, ' ').toLowerCase().replace(/\b\w/g, c => c.toUpperCase());
}

function checkAuth(req) {
    // En Node, los headers suelen estar en minúscula
    const auth = req.headers.authorization || req.headers.Authorization;
    const adminPassword = process.env.ADMIN_PASSWORD;
    if (!auth || !adminPassword) return false;
    
    // Usamos Buffer en lugar del btoa() de los navegadores
    const expectedAuth = `Basic ${Buffer.from(`admin:${adminPassword}`).toString('base64')}`;
    
    if (auth.length !== expectedAuth.length) {
        return false;
    }

    let isMatch = 0;
    for (let i = 0; i < expectedAuth.length; i++) {
        isMatch |= auth.charCodeAt(i) ^ expectedAuth.charCodeAt(i);
    }
    
    return isMatch === 0;
}

export async function handleAdminRequest(req, res) {
    // En Node req.url es solo el path (ej: "/admin")
    const path = req.url ? req.url.split('?')[0] : "";

    // Si no es una ruta de admin, devolvemos false para que index.js siga con las rutas públicas
    if (!path.startsWith("/admin") && !path.startsWith("/api/origin")) {
        return false; 
    }

    if (!checkAuth(req)) {
        res.setHeader("WWW-Authenticate", 'Basic realm="Admin Area"');
        res.status(401).send("Unauthorized. Por favor, inicie sesión.");
        return true; // true significa "ya respondí, index.js no hagas nada más"
    }

    if (path === "/admin/style.css") {
        res.setHeader("Content-Type", "text/css; charset=UTF-8");
        res.status(200).send(adminCss);
        return true;
    }

    if (path === "/admin") {
        const html = `<!DOCTYPE html>
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
                <input type="text" id="province" list="provincesList" placeholder="Provincia (ej: Santa Cruz)" required />
                <input type="text" id="city" placeholder="Ciudad (ej: Rosario)" required />
                <button type="submit" id="submitBtn" class="submit-btn">Guardar Registro</button>
            </form>
            <datalist id="provincesList"></datalist>
        </div>
        <div class="card">
            <h3>Registros Actuales</h3>
            <button onclick="loadChannels()" style="margin-bottom: 15px;">Refrescar Lista</button>
            <div class="scroll-wrapper">
                <table id="channelsTable">
                    <thead><tr><th>ID</th><th>Título</th><th>Categoría</th><th>Provincia</th><th>Ciudad</th><th>Acciones</th></tr></thead>
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
                <td class="col-province">\${c.province || ''}</td>
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
            row.querySelector('.col-province').innerHTML = \`<input type="text" id="edit-province-\${id}" list="provincesList" value="\${channel.province || ''}" />\`;
            row.querySelector('.col-city').innerHTML = \`<input type="text" id="edit-city-\${id}" value="\${channel.city || ''}" />\`;
            
            row.querySelector('.col-actions').innerHTML = \`
                <button class="btn-save" onclick="saveChannelInline('\${id}')">Guardar</button>
                <button class="btn-cancel" onclick="loadChannels()">Cancelar</button>
            \`;
        }

        async function saveChannelInline(id) {
            const title = document.getElementById(\`edit-title-\${id}\`).value;
            const category = document.getElementById(\`edit-category-\${id}\`).value;
            const province = document.getElementById(\`edit-province-\${id}\`).value;
            const city = document.getElementById(\`edit-city-\${id}\`).value;
            
            await fetch('/api/origin/channels', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ _id: id, title, category, province, city })
            });
            loadChannels();
            loadProvinces();
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
            const province = document.getElementById('province').value;
            const city = document.getElementById('city').value;
            
            const payload = { title, category, province, city };
            if (_id) payload._id = _id; 

            await fetch('/api/origin/channels', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            e.target.reset();
            loadChannels();
            loadProvinces();
        });

        async function loadProvinces() {
            const res = await fetch('/api/origin/provinces');
            if (res.ok) {
                const provinces = await res.json();
                const list = document.getElementById('provincesList');
                list.innerHTML = provinces.map(p => \`<option value="\${p.name}">\`).join('');
            }
        }

        loadChannels();
        loadProvinces();
    </script>
</body>
</html>`;
        
        res.setHeader("Content-Type", "text/html; charset=UTF-8");
        res.status(200).send(html);
        return true;
    }

    if (path === "/api/origin/provinces") {
        try {
            const client = await getMongoClient();
            const db = client.db("zappingstreamdb");
            const collection = db.collection("provinces");

            if (req.method === "GET") {
                const provinces = await collection.find({}).toArray();
                res.status(200).json(provinces);
                return true;
            }
        } catch (error) {
            res.status(500).json({ error: error.message });
            return true;
        }
    }

    if (path === "/api/origin/channels") {
        try {
            const client = await getMongoClient();
            const db = client.db("zappingstreamdb");
            const collection = db.collection("origin");

            const upsertProvinceCity = async (provinceRaw, cityRaw) => {
                const province = normalizeName(provinceRaw || "");
                const city = normalizeName(cityRaw || "");
                if (!provinceRaw || !cityRaw) return { province, city };
                await db.collection("provinces").updateOne(
                    { name: province },
                    { $addToSet: { cities: city } },
                    { upsert: true }
                );
                return { province, city };
            };

            if (req.method === "GET") {
                const channels = await collection.find({}).toArray();
                res.status(200).json(channels);
                return true;
            }

            if (req.method === "POST") {
                // En Vercel el JSON del body ya viene parseado
                const body = req.body; 
                const { province, city } = await upsertProvinceCity(body.province, body.city);
                body.province = province;
                body.city = city;
                const result = await collection.insertOne(body);
                if (city && province) await collection.updateMany({ city: city, province: { $ne: province } }, { $set: { province: province } });
                res.status(201).json({ success: true, insertedId: result.insertedId });
                return true;
            }

            if (req.method === "PUT") {
                const body = req.body;
                if (!body._id) {
                    res.status(400).json({ error: "Falta el _id para actualizar" });
                    return true;
                }
                const { _id, ...updateFields } = body;
                const { province, city } = await upsertProvinceCity(updateFields.province, updateFields.city);
                updateFields.province = province;
                updateFields.city = city;
                const result = await collection.updateOne({ _id }, { $set: updateFields });
                if (updateFields.city && updateFields.province) await collection.updateMany({ city: updateFields.city, province: { $ne: updateFields.province } }, { $set: { province: updateFields.province } });
                res.status(200).json({ success: true, modifiedCount: result.modifiedCount });
                return true;
            }

            if (req.method === "DELETE") {
                const body = req.body;
                if (!body._id) {
                    res.status(400).json({ error: "Falta el _id para eliminar" });
                    return true;
                }
                const result = await collection.deleteOne({ _id: body._id });
                res.status(200).json({ success: true, deletedCount: result.deletedCount });
                return true;
            }
        } catch (error) {
            res.status(500).json({ error: error.message });
            return true;
        }
    }

    return false; // No interceptó nada
}
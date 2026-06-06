// OJO: Si en el paso anterior moviste estos archivos a la carpeta "utils",
// acordate de cambiar las rutas a "../utils/mongodb.js"
import { getMongoClient } from "./mongodb.js";
import { handleAdminRequest } from "./baseadmin.js";

export default async function handler(req, res) {
    // 1. CORS adaptado a Node.js (modificando los headers de la respuesta)
    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Methods", "GET,HEAD,POST,OPTIONS");
    res.setHeader("Access-Control-Max-Age", "86400");
    res.setHeader("Access-Control-Allow-Headers", req.headers["access-control-request-headers"] || "*");

    if (req.method === "OPTIONS") {
        return res.status(200).end();
    }

    // En Vercel, req.url te devuelve la ruta que pidió el cliente (ej: "/api/channels")
    const path = req.url || "";

    // --- DELEGAR A BASEADMIN ---
    // IMPORTANTE: Tu baseadmin.js seguro está escrito a lo Cloudflare también.
    // Va a fallar si entrás a administrar, pero atajamos el error para que 
    // no te tire abajo las rutas públicas ahora mismo.
    try {
        const adminHandled = await handleAdminRequest(req, res);
        if (adminHandled) return; 
    } catch (e) {
        console.error("Ignorando error de baseadmin temporalmente", e);
    }

    // --- RUTAS PÚBLICAS ---
    if (path.includes("/channels")) {
        try {
            const client = await getMongoClient();
            const db = client.db("zappingstreamdb");
            const channels = await db.collection("channels").find({}).toArray();
            
            return res.status(200).json(channels);
        } catch (error) {
            return res.status(500).json({ error: error.message });
        }
    }

    if (path.includes("/provinces")) {
        try {
            const client = await getMongoClient();
            const db = client.db("zappingstreamdb");
            const provinces = await db.collection("provinces").find({}).toArray();
            
            return res.status(200).json(provinces);
        } catch (error) {
            return res.status(500).json({ error: error.message });
        }
    }

    return res.status(200).send("Zapping Streaming API - Hello World!");
}
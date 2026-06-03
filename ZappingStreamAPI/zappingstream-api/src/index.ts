/**
 * Welcome to Cloudflare Workers! This is your first worker.
 *
 * - Run `npm run dev` in your terminal to start a development server
 * - Open a browser tab at http://localhost:8787/ to see your worker in action
 * - Run `npm run deploy` to publish your worker
 *
 * Bind resources to your worker in `wrangler.jsonc`. After adding bindings, a type definition for the
 * `Env` object can be regenerated with `npm run cf-typegen`.
 *
 * Learn more at https://developers.cloudflare.com/workers/
 */

import { MongoClient } from "mongodb";
import { handleAdminRequest } from "./baseadmin";

const corsHeaders = {
	"Access-Control-Allow-Origin": "*",
	"Access-Control-Allow-Methods": "GET,HEAD,POST,OPTIONS",
	"Access-Control-Max-Age": "86400",
};

export default {
	async fetch(request, env, ctx): Promise<Response> {
		if (request.method === "OPTIONS") {
			return new Response(null, {
				headers: {
					...corsHeaders,
					"Access-Control-Allow-Headers": request.headers.get("Access-Control-Request-Headers") || "",
				},
			});
		}

		const url = new URL(request.url);

		// --- DELEGAR A BASEADMIN ---
		const adminResponse = await handleAdminRequest(request, env, ctx, corsHeaders);
		if (adminResponse) {
			return adminResponse;
		}

		// --- RUTAS PÚBLICAS ---
		if (url.pathname === "/channels") {
			if (!env.MONGODB_URI) {
				return Response.json({ error: "Falta la variable de entorno MONGODB_URI" }, { status: 500, headers: corsHeaders });
			}

			const client = new MongoClient(env.MONGODB_URI);

			try {
				await client.connect();

				const db = client.db("zappingstreamdb");

				const channels = await db
					.collection("channels")
					.find({})
					.toArray();

				return Response.json(channels, { headers: corsHeaders });
			} catch (error: any) {
				return Response.json({ error: error.message }, { status: 500, headers: corsHeaders });
			} finally {
				// Cerramos la conexión para que Cloudflare no detecte la petición como colgada
				ctx.waitUntil(client.close());
			}
		}
		return new Response("Hello World!", { headers: corsHeaders });
	},
} satisfies ExportedHandler<Env>;

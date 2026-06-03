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

				// Filtrar los videos de Past, Actives y Upcoming para quitar los que tienen ToBeCut: true
				const filteredChannels = channels.map((channel: any) => {
					// Función auxiliar para descartar los videos a cortar (soporta arreglos y objetos)
					const filterVideos = (videoData: any) => {
						if (!videoData || typeof videoData !== "object") return videoData;

						// Si resulta ser un arreglo
						if (Array.isArray(videoData)) {
							return videoData.filter((video: any) => video.ToBeCut !== true && video.ToBeCut !== "true");
						}

						const filtered: any = {};
						for (const [key, video] of Object.entries(videoData)) {
							if ((video as any).ToBeCut !== true && (video as any).ToBeCut !== "true") {
								filtered[key] = video;
							}
						}
						return filtered;
					};

					const updated = { ...channel };

					if (updated.Past) updated.Past = filterVideos(updated.Past);
					if (updated.Actives) updated.Actives = filterVideos(updated.Actives);
					if (updated.Upcoming) updated.Upcoming = filterVideos(updated.Upcoming);


					return updated;
				});

				return Response.json(filteredChannels, { headers: corsHeaders });
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

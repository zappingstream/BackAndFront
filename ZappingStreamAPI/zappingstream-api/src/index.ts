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

export default {
	async fetch(request, env, ctx): Promise<Response> {
		const url = new URL(request.url);
		if (url.pathname === "/channels") {
			if (!env.MONGODB_URI) {
				return Response.json({ error: "Falta la variable de entorno MONGODB_URI" }, { status: 500 });
			}

			const client = new MongoClient(env.MONGODB_URI);

			try {
				await client.connect();

				const db = client.db("zappingstreamdb");

				const channels = await db
					.collection("channels")
					.find({})
					.toArray();

				return Response.json(channels);
			} catch (error: any) {
				return Response.json({ error: error.message }, { status: 500 });
			} finally {
				// Cerramos la conexión para que Cloudflare no detecte la petición como colgada
				ctx.waitUntil(client.close());
			}
		}
		return new Response("Hello World!");
	},
} satisfies ExportedHandler<Env>;

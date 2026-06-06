import { MongoClient } from "mongodb";

let cachedClient = null;

export async function getMongoClient() {
	if (cachedClient) {
		return cachedClient;
	}

	if (!process.env.MONGODB_URI) {
		throw new Error("Falta la variable de entorno MONGODB_URI");
	}

	const client = new MongoClient(process.env.MONGODB_URI);
	await client.connect();
	cachedClient = client;
	
	return client;
}
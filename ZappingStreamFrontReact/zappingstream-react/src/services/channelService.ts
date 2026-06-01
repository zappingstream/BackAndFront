import type { Channel } from '../models/Channel';




const API_URL = 'https://channels.zappingstream.com/channels';

export const getChannels = async (): Promise<Channel[]> => {
  try {
    // 4. Obtener el token validado por reCAPTCHA

    // 5. Adjuntar el token en los headers de tu fetch original
    const response = await fetch(API_URL, {

    });

    if (!response.ok) {
      throw new Error(`Error HTTP: ${response.status} - ${response.statusText}`);
    }

    const data: Channel[] = await response.json();

    if (!data) {
      return [];
    }

    return data;

  } catch (error) {
    console.error("Error al obtener los canales:", error);
    throw error;
  }
};
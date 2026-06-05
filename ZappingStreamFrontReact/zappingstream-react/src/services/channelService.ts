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

    // Recorremos los canales y filtramos los videos internos con ToBeCut = true
    return data.map(channel => {
      const filterVideos = (videos: any) => {
        if (!videos) return videos;
        // Soportar tanto arrays como diccionarios (objetos)
        if (Array.isArray(videos)) {
          return videos.filter((v: any) => v.ToBeCut !== true);
        }
        return Object.fromEntries(
          Object.entries(videos).filter(([_, v]: [string, any]) => v.ToBeCut !== true)
        );
      };

      return {
        ...channel,
        Actives: filterVideos(channel.Actives),
        Upcoming: filterVideos(channel.Upcoming),
        Past: filterVideos(channel.Past)
      };
    });

  } catch (error) {
    console.error("Error al obtener los canales:", error);
    throw error;
  }
};
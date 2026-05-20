import type { Channel } from '../models/Channel';

const FIREBASE_URL = 'https://zappingstreaming-default-rtdb.firebaseio.com/Channels.json';

export const getChannels = async (): Promise<Channel[]> => {
  try {
    const response = await fetch(FIREBASE_URL);
    
    if (!response.ok) {
      throw new Error(`Error HTTP: ${response.status} - ${response.statusText}`);
    }

    const data: Record<string, Channel> = await response.json();

    if (!data) {
      return [];
    }

    const channelsArray = Object.values(data);
    
    return channelsArray;
    
  } catch (error) {
    console.error("Error al obtener los canales de Firebase RTDB:", error);
    throw error;
  }
};
import type { Channel } from '../models/Channel';
import { initializeApp } from 'firebase/app';
import { initializeAppCheck, ReCaptchaV3Provider, getToken } from 'firebase/app-check';

const firebaseConfig = {
  apiKey: "AIzaSyAnymUVsnZdWfjRWgh0uLxaVmW4zb6GZWE",
  authDomain: "zappingstreaming.firebaseapp.com",
  databaseURL: "https://zappingstreaming-default-rtdb.firebaseio.com",
  projectId: "zappingstreaming",
  storageBucket: "zappingstreaming.firebasestorage.app",
  messagingSenderId: "593976978663",
  appId: "1:593976978663:web:c57204672e0ed0a5708642",
  measurementId: "G-6FGQ48456S"
};

const app = initializeApp(firebaseConfig);

const appCheck = initializeAppCheck(app, {
  provider: new ReCaptchaV3Provider('6LfZT7ssAAAAADaZNAxdBH9qvGJuceUt3Yzwb_hz'),
  isTokenAutoRefreshEnabled: true
});

const FIREBASE_URL = 'https://zappingstreaming-default-rtdb.firebaseio.com/Channels.json';

export const getChannels = async (): Promise<Channel[]> => {
  try {
    // 4. Obtener el token validado por reCAPTCHA
    const appCheckTokenResponse = await getToken(appCheck, false);

    // 5. Adjuntar el token en los headers de tu fetch original
    const response = await fetch(FIREBASE_URL, {
      headers: {
        'X-Firebase-AppCheck': appCheckTokenResponse.token
      }
    });

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
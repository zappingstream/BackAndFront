import { useState, useEffect, useCallback } from 'react';
import type { Channel } from '../models/Channel';
import { getChannels } from '../services/channelService';

export const useChannels = () => {
  const [channels, setChannels] = useState<Channel[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchChannels = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await getChannels();
      setChannels(data);
    } catch (err) {
      setError('Ocurrió un error al cargar los canales.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchChannels();
  }, [fetchChannels]);

  return { channels, isLoading, error, refetch: fetchChannels };
};
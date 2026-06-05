import { useState, useEffect, useCallback } from 'react';
import { getProvinces, type CityProvinceMap } from '../services/provinceService';

export const useLocations = () => {
  const [cityProvinceMap, setCityProvinceMap] = useState<CityProvinceMap>({});
  const [provinces, setProvinces] = useState<string[]>([]);
  const [isLoadingLocations, setIsLoadingLocations] = useState(false);

  const fetchLocations = useCallback(async () => {
    setIsLoadingLocations(true);
    try {
      const data = await getProvinces();
      setCityProvinceMap(data.cityProvinceMap);
      setProvinces(data.provinces);
    } catch (error) {
      console.error("Error fetching locations:", error);
    } finally {
      setIsLoadingLocations(false);
    }
  }, []);

  useEffect(() => {
    fetchLocations();
  }, [fetchLocations]);

  return { cityProvinceMap, provinces, isLoadingLocations };
};
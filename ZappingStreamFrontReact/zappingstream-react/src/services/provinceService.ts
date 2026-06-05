export interface CityProvinceMap {
  [city: string]: string;
}

export interface ProvinceResponse {
  _id: string;
  name: string;
  cities: string[];
}

const API_URL = 'https://channels.zappingstream.com/provinces';

export const getProvinces = async (): Promise<{ cityProvinceMap: CityProvinceMap, provinces: string[] }> => {
  try {
    const response = await fetch(API_URL);

    if (!response.ok) {
      throw new Error(`Error HTTP: ${response.status} - ${response.statusText}`);
    }

    const data: ProvinceResponse[] = await response.json();
    
    const cityProvinceMap: CityProvinceMap = {};
    const provinces: string[] = [];
    
    data.forEach(prov => {
      provinces.push(prov.name);
      prov.cities.forEach(city => {
        cityProvinceMap[city] = prov.name;
      });
    });
    
    return { cityProvinceMap, provinces: provinces.sort() };
    
  } catch (error) {
    console.error("Error al obtener las provincias:", error);
    throw error;
  }
};
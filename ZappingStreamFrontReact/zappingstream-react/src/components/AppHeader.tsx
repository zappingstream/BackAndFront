import './AppHeader.css';
import logo from '../assets/logo.png';

interface AppHeaderProps {
    searchText: string;
    onSearchChange: (text: string) => void;
    onRefresh: () => void;
    isRefreshing: boolean;
    viewMode: 'cards' | 'grid';
    onViewModeChange: (mode: 'cards' | 'grid') => void;
    selectedProvince: string;
    onProvinceChange: (prov: string) => void;
    selectedCity: string;
    onCityChange: (city: string) => void;
    provinces: string[];
    cities: string[];
}

export const AppHeader = ({
    searchText,
    onSearchChange,
    viewMode,
    onViewModeChange,
    selectedProvince,
    onProvinceChange,
    selectedCity,
    onCityChange,
    provinces,
    cities
}: AppHeaderProps) => {
    return (
        <div className="sticky-top-section">
            <div className="top-bar-container">
                <header className="zapping-header">
                    <img src={logo} alt="Zapping Stream" className="app-logo" />
                </header>

                <div className="header-controls">                  
                    <div className="filters-container">
                        <input
                            type="text"
                            className="search-input"
                            placeholder="Buscar por canal..."
                            value={searchText}
                            onChange={(e) => onSearchChange(e.target.value)}
                        />
                        <select 
                            className="filter-select"
                            value={selectedProvince}
                            onChange={(e) => onProvinceChange(e.target.value)}
                        >
                            <option value="">Provincia: Todas</option>
                            {provinces.map(prov => (
                                <option key={prov} value={prov}>{prov}</option>
                            ))}
                        </select>
                        <select 
                            className="filter-select"
                            value={selectedCity}
                            onChange={(e) => onCityChange(e.target.value)}
                            disabled={cities.length === 0}
                        >
                            <option value="">Ciudad: Todas</option>
                            {cities.sort().map(city => (
                                <option key={city} value={city}>{city}</option>
                            ))}
                        </select>
                    </div>

                    <div className="view-mode-container">
                        <button 
                            className={`view-mode-btn ${viewMode === 'cards' ? 'active' : ''}`}
                            onClick={() => onViewModeChange('cards')}
                        >
                            Canales
                        </button>
                        <button 
                            className={`view-mode-btn ${viewMode === 'grid' ? 'active' : ''}`}
                            onClick={() => onViewModeChange('grid')}
                        >
                            Transmisiones
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};
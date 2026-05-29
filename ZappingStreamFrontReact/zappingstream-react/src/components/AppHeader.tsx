import './AppHeader.css';
import logo from '../assets/logo.png';

interface AppHeaderProps {
    searchText: string;
    onSearchChange: (text: string) => void;
    onRefresh: () => void;
    isRefreshing: boolean;
    onShowInfo: () => void;
    viewMode: 'cards' | 'grid';
    onViewModeChange: (mode: 'cards' | 'grid') => void;
}

export const AppHeader = ({
    searchText,
    onSearchChange,
    onRefresh,
    isRefreshing,
    onShowInfo,
    viewMode,
    onViewModeChange,
}: AppHeaderProps) => {
    return (
        <div className="sticky-top-section">
            <button className="info-btn" title="¿Cómo funciona?" onClick={onShowInfo}>?</button>
            <header className="zapping-header">
                <img src={logo} alt="Zapping Stream" className="app-logo" />
            </header>

            <div className="search-container">
                <input
                    type="text"
                    className="search-input"
                    placeholder="Buscar por canal o ciudad..."
                    value={searchText}
                    onChange={(e) => onSearchChange(e.target.value)}
                />
                <button
                    className="refresh-btn"
                    title="Actualizar canales"
                    onClick={onRefresh}
                    disabled={isRefreshing}
                >
                    ↻
                </button>
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
                    Grilla Horaria
                </button>
            </div>
        </div>
    );
};
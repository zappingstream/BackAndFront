import './StatusDisplay.css';

interface StatusDisplayProps {
    isFetching: boolean;
    isLoading: boolean;
    hasChannels: boolean;
    hasFilteredChannels: boolean;
    searchText: string;
    hasActiveFilters?: boolean;
}

export const StatusDisplay = ({ isFetching, isLoading, hasChannels, hasFilteredChannels, searchText, hasActiveFilters }: StatusDisplayProps) => {
    if (isFetching || isLoading) {
        return (
            <div className="status-message">
                <div className="spinner"></div>
                <p>Conectando con el universo del stream argentino...</p>
            </div>
        );
    }

    if (!hasChannels) {
        return (
            <div className="status-message">
                <p>No se encontraron canales configurados.</p>
            </div>
        );
    }

    if (!hasFilteredChannels && (searchText || hasActiveFilters)) {
        return (
            <div className="status-message">
                <p>No hay resultados para la búsqueda actual.</p>
            </div>
        );
    }

    return null;
};
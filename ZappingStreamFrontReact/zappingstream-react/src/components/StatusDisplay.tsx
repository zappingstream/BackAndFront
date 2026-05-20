import './StatusDisplay.css';

interface StatusDisplayProps {
    isFetching: boolean;
    isLoading: boolean;
    hasChannels: boolean;
    hasFilteredChannels: boolean;
    searchText: string;
}

export const StatusDisplay = ({ isFetching, isLoading, hasChannels, hasFilteredChannels, searchText }: StatusDisplayProps) => {
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
                <p>No se encontraron canales configurados en Firebase.</p>
            </div>
        );
    }

    if (!hasFilteredChannels && searchText) {
        return (
            <div className="status-message">
                <p>No hay resultados para "{searchText}"</p>
            </div>
        );
    }

    return null;
};
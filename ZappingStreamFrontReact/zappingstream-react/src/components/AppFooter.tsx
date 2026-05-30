import './AppFooter.css';

interface AppFooterProps {
    onRefresh: () => void;
    isRefreshing: boolean;
    onShowInfo: () => void;
}

export const AppFooter = ({ onRefresh, isRefreshing, onShowInfo }: AppFooterProps) => {
    return (
        <div className="floating-footer">
            <button className="footer-action-btn" onClick={onRefresh} disabled={isRefreshing}>
                <span className={`refresh-icon ${isRefreshing ? 'spin' : ''}`}>↻</span>
                <span>Actualizar</span>
            </button>
            <div className="footer-divider"></div>
            <button className="footer-action-btn" onClick={onShowInfo}>
                <span>Info / Contacto</span>
            </button>
        </div>
    );
};

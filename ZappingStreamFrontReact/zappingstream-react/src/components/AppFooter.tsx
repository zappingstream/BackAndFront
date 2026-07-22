import { useState, useEffect } from 'react';
import './AppFooter.css';

interface AppFooterProps {
    onRefresh: () => void;
    isRefreshing: boolean;
    onShowInfo: () => void;
}

export const AppFooter = ({ onRefresh, isRefreshing, onShowInfo }: AppFooterProps) => {
    const [needsUpdate, setNeedsUpdate] = useState(false);

    useEffect(() => {
        if (needsUpdate) return;
        
        const timer = setTimeout(() => {
            setNeedsUpdate(true);
        }, 300000); // 5 minutos

        return () => clearTimeout(timer);
    }, [needsUpdate]);

    const handleRefresh = () => {
        setNeedsUpdate(false);
        onRefresh();
    };

    return (
        <div className="floating-footer">
            <button className={`footer-action-btn ${needsUpdate ? 'needs-update' : ''}`} onClick={handleRefresh} disabled={isRefreshing}>
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

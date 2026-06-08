import React, { useState, useEffect } from 'react';
import './VideoCard.css';

interface VideoCardProps {
    imageUrl?: string;
    altText?: string;
    fallbackText?: string;
    isLive?: boolean;
    isPremiere?: boolean;
    isPast?: boolean;
    isUpcoming?: boolean;
    onClick?: (e: React.MouseEvent<HTMLDivElement>) => void;
    className?: string;
    imageClassName?: string;
    onImageError?: () => void;
}

export const VideoCard = ({ imageUrl, altText, fallbackText, isLive, isPremiere, isPast, isUpcoming, onClick, className = "", imageClassName = "channel-logo", onImageError }: VideoCardProps) => {
    const [hasError, setHasError] = useState(false);

    useEffect(() => {
        setHasError(false);
    }, [imageUrl]);

    const handleError = () => {
        setHasError(true);
        if (onImageError) {
            onImageError();
        }
    };

    const handleLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
        const img = e.currentTarget;
        // Detecta si YouTube devolvió su clásica imagen gris genérica (120x90) en lugar de un error 404
        if (img.naturalWidth === 120 && img.naturalHeight === 90 && imageUrl?.includes('ytimg.com')) {
            handleError();
        }
    };

    return (
        <div className={`image-container ${className} ${isPast ? "past-video-card" : ""}`} onClick={onClick} style={{ cursor: onClick ? 'pointer' : 'default' }}>
            {imageUrl && !hasError ? (
                <img 
                    src={imageUrl} 
                    alt={altText || ""} 
                    className={imageClassName} 
                    loading="lazy" 
                    referrerPolicy="no-referrer" 
                    onError={handleError}
                    onLoad={handleLoad}
                />
            ) : (
                <div className="fallback-logo">
                    <span>{fallbackText ? fallbackText.substring(0, 1).toUpperCase() : "?"}</span>
                </div>
            )}
            {isLive && (
                isPremiere ? (
                    <div className="badge-estreno"><span className="punto-azul"></span> ESTRENO</div>
                ) : (
                    <div className="badge-vivo"><span className="punto-rojo"></span> EN VIVO</div>
                )
            )}
            {isPast && (
                isPremiere ? (
                    <div className="badge-past premiere">ESTRENADO</div>
                ) : (
                    <div className="badge-past live">FINALIZADO</div>
                )
            )}
            {isUpcoming && !isLive && !isPast && (
                isPremiere ? (
                    <div className="badge-upcoming premiere">ESTRENO PROG.</div>
                ) : (
                    <div className="badge-upcoming live">PROGRAMADO</div>
                )
            )}
        </div>
    );
};
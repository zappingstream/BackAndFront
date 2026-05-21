import React from 'react';
import './VideoCard.css';

interface VideoCardProps {
    imageUrl?: string;
    altText?: string;
    fallbackText?: string;
    isLive?: boolean;
    isPremiere?: boolean;
    onClick?: (e: React.MouseEvent<HTMLDivElement>) => void;
    className?: string;
    imageClassName?: string;
}

export const VideoCard = ({ imageUrl, altText, fallbackText, isLive, isPremiere, onClick, className = "", imageClassName = "channel-logo" }: VideoCardProps) => {
    return (
        <div className={`image-container ${className}`} onClick={onClick} style={{ cursor: onClick ? 'pointer' : 'default' }}>
            {imageUrl ? (
                <img src={imageUrl} alt={altText || ""} className={imageClassName} loading="lazy" referrerPolicy="no-referrer" />
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
        </div>
    );
};
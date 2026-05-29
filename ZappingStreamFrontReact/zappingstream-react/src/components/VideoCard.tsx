import React from 'react';
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
}

export const VideoCard = ({ imageUrl, altText, fallbackText, isLive, isPremiere, isPast, isUpcoming, onClick, className = "", imageClassName = "channel-logo" }: VideoCardProps) => {
    return (
        <div className={`image-container ${className} ${isPast ? "past-video-card" : ""}`} onClick={onClick} style={{ cursor: onClick ? 'pointer' : 'default' }}>
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
            {isPast && (
                <div className="badge-past">FINALIZADO</div>
            )}
            {isUpcoming && !isLive && !isPast && (
                isPremiere ? (
                    <div className="badge-upcoming">ESTRENO PROG.</div>
                ) : (
                    <div className="badge-upcoming">PROGRAMADO</div>
                )
            )}
        </div>
    );
};
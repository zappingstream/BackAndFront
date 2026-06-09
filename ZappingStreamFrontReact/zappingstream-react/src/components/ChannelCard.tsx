import { useState } from 'react';
import type { Channel } from '../models/Channel';
import { formatActivityDate, getFreshImage } from '../index';
import { VideoCard } from './VideoCard';
import './ChannelCard.css';

interface ChannelCardProps {
    channel: Channel;
    isExpanded: boolean;
    isLiveGroup: boolean;
    toggleInfo: (channelName: string) => void;
    abrirCanal: (channel: Channel) => void;
    abrirCanalOnStreams: (channel: Channel) => void;
    abrirCanalOnDemand: (channel: Channel) => void;
    navigateYouTube: (url: string) => void;
}

export const ChannelCard = ({
    channel,
    isExpanded,
    isLiveGroup,
    toggleInfo,
    abrirCanal,
    abrirCanalOnStreams,
    abrirCanalOnDemand,
    navigateYouTube,
}: ChannelCardProps) => {
    const [failedVideos, setFailedVideos] = useState<Set<string>>(new Set());

    // Extraer y ordenar los videos activos del más reciente al más antiguo
    const activeVideos = isLiveGroup && channel.Actives
        ? Object.values(channel.Actives).filter(v => !failedVideos.has(v.VideoId)).sort((a, b) => {
            // Precedencia: Vivo (no estreno) por sobre Estreno
            if (a.IsPremiere && !b.IsPremiere) return 1;
            if (!a.IsPremiere && b.IsPremiere) return -1;

            const timeA = new Date(a.ActualStartTime || a.ScheduledStartTime || a.AddedAt || 0).getTime();
            const timeB = new Date(b.ActualStartTime || b.ScheduledStartTime || b.AddedAt || 0).getTime();
            return timeB - timeA;
        })
        : [];

    const mainActive = activeVideos.length > 0 ? activeVideos[0] : null;
    const restoActivos = activeVideos.slice(1);

    const cardWidthPx = isExpanded ? 0 : (isLiveGroup ? 320 + (restoActivos.length * 295) : 320);
    const cardStyle = isExpanded ? {} : { width: `${cardWidthPx}px` };

    const primaryImageUrl = mainActive && mainActive.ThumbnailUrl
        ? getFreshImage(mainActive.ThumbnailUrl, channel.LastActivityAt)
        : channel.ChannelImgUrl;

    const renderHeader = () => (
        <div 
            className={`card-header ${isLiveGroup ? 'clickable' : ''}`} 
            onClick={isLiveGroup ? () => abrirCanal(channel) : undefined} 
        >
            <div className="title-group">
                {channel.ChannelImgUrl && (isExpanded || (isLiveGroup && mainActive)) && (
                    <img src={channel.ChannelImgUrl} alt="Logo" className="header-mini-logo" loading="lazy" />
                )}
                <h3 className="channel-title">{channel.ChannelName}</h3>
            </div>
            <button
                className="toggle-info-btn"
                onClick={(e) => { e.stopPropagation(); toggleInfo(channel.ChannelName); }}
            >
                {isExpanded ? "Ocultar" : "Info"}
            </button>
        </div>
    );

   const renderExpandedBody = () => (
        <>
            <VideoCard 
                className="banner-container"
                imageUrl={channel.ChannelBannerUrl ? `${channel.ChannelBannerUrl}=w1707-fcrop64=1,00005a57ffffa5a8-k-c0xffffffff-no-nd-rj` : channel.ChannelImgUrl}
                altText={channel.ChannelName}
                fallbackText={channel.ChannelName}
                onClick={() => abrirCanal(channel)}
                imageClassName="channel-banner"
            />
            <div className="channel-description" onClick={(e) => e.stopPropagation()}>
                <h4 className="full-title-info">{channel.ChannelName}</h4>
                {channel.ChannelCity && <p className="city-tag">📍 {channel.ChannelCity}</p>}
                <p>{channel.ChannelDescription || "Sin descripción disponible."}</p>
            </div>
            <button className="submit-btn" onClick={() => abrirCanalOnDemand(channel)}>
                Ir al Canal (On-Demand)
            </button>
        </>
    );

    const renderLiveBody = () => {
        if (!mainActive) return renderOnDemandBody();
        
        return (
            <div className="videos-horizontal-list">
                <VideoCard 
                    className="primary-video"
                    imageUrl={primaryImageUrl}
                    altText={mainActive.Title || channel.ChannelName}
                    fallbackText={channel.ChannelName}
                    isLive={mainActive.Live !== false}
                    isPremiere={mainActive.IsPremiere}
                    onClick={() => abrirCanal(channel)}
                    onImageError={() => setFailedVideos(prev => new Set(prev).add(mainActive.VideoId))}
                />
                {restoActivos.map(activo => (
                    <VideoCard
                        key={activo.VideoId}
                        className="secondary-video"
                        imageUrl={activo.ThumbnailUrl ? getFreshImage(activo.ThumbnailUrl, channel.LastActivityAt) : undefined}
                        altText={activo.Title}
                        fallbackText={channel.ChannelName}
                        isLive={true}
                        isPremiere={activo.IsPremiere}
                        onClick={(e) => { e.stopPropagation(); navigateYouTube(`https://www.youtube.com/watch?v=${activo.VideoId}`); }}
                        onImageError={() => setFailedVideos(prev => new Set(prev).add(activo.VideoId))}
                    />
                ))}
            </div>
        );
    };

    const renderOnDemandBody = () => (
        <VideoCard 
            imageUrl={channel.ChannelImgUrl}
            altText={channel.ChannelName}
            fallbackText={channel.ChannelName}
        />
    );

    const renderFooter = () => (
        <div 
            className={`last-activity-container ${isLiveGroup ? 'clickable' : ''}`} 
            onClick={isLiveGroup ? () => abrirCanal(channel) : undefined} 
        >
            <span className="last-activity-text">{formatActivityDate(channel.LastActivityAt)}</span>
        </div>
    );

    return (
        <div className="card-wrapper" style={cardStyle}>
            <div 
                className={`channel-card ${isExpanded ? "expanded-card" : ""}`} 
                tabIndex={0}
                onClick={!isLiveGroup && !isExpanded ? () => abrirCanalOnStreams(channel) : undefined}
            >
                {renderHeader()}
                {isExpanded && renderExpandedBody()}
                {!isExpanded && isLiveGroup && renderLiveBody()}
                {!isExpanded && !isLiveGroup && renderOnDemandBody()}
                {!isExpanded && renderFooter()}
            </div>
        </div>
    );
};
import { useState, useEffect } from 'react';
import { useHorizontalScroll } from '../hooks/useHorizontalScroll';
import type { Channel } from '../models/Channel';
import { ChannelCard } from './ChannelCard';
import './ChannelCategoryRow.css';

interface ChannelCategoryProps {
    title: string;
    channels: Channel[];
    expandedChannels: Set<string>;
    toggleInfo: (channelName: string) => void;
    abrirCanal: (channel: Channel) => void;
    abrirCanalOnStreams: (channel: Channel) => void;
    abrirCanalOnDemand: (channel: Channel) => void;
    navigateYouTube: (url: string) => void;
}

export const ChannelCategoryRow = ({
    title,
    channels,
    expandedChannels,
    toggleInfo,
    abrirCanal,
    abrirCanalOnStreams,
    abrirCanalOnDemand,
    navigateYouTube,
}: ChannelCategoryProps) => {
    const scrollRef = useHorizontalScroll();
    const [canScrollLeft, setCanScrollLeft] = useState(false);
    const [canScrollRight, setCanScrollRight] = useState(true);

    const checkScroll = () => {
        if (scrollRef.current) {
            const { scrollLeft, scrollWidth, clientWidth } = scrollRef.current;
            setCanScrollLeft(scrollLeft > 0);
            setCanScrollRight(Math.ceil(scrollLeft) < scrollWidth - clientWidth - 1);
        }
    };

    useEffect(() => {
        checkScroll();
        window.addEventListener('resize', checkScroll);

        const observer = new MutationObserver(() => {
            checkScroll();
        });
        if (scrollRef.current) {
            observer.observe(scrollRef.current, { childList: true, subtree: true });
        }

        return () => {
            window.removeEventListener('resize', checkScroll);
            observer.disconnect();
        };
    }, [channels, expandedChannels]);

    const canalesEnVivo = channels.filter(c => c.Actives && Object.keys(c.Actives).length > 0);
    const canalesOnDemand = channels.filter(c => !c.Actives || Object.keys(c.Actives).length === 0);

    const isExpanded = (name: string) => expandedChannels.has(name);

    return (
        <div key={title}>
            <h2 className="category-title">{title}</h2>
            <div className="scroll-wrapper category-scroll-wrapper">
                <button className={`scroll-arrow left-arrow ${!canScrollLeft ? 'disabled' : ''}`} onClick={() => scrollRef.current?.scrollBy({ left: -window.innerWidth / 2, behavior: 'smooth' })}>‹</button>
                
                <div className="channel-row" ref={scrollRef} onScroll={checkScroll}>
                {/* GRUPO EN VIVO */}
                {canalesEnVivo.length > 0 && (
                    <div className="status-group">
                        <div className="videostatus">
                            <span className="videostatusspan">AHORA</span>
                            <div className="videostatusend"></div>
                        </div>
                        <div className="cards-container">
                                    {canalesEnVivo.map(channel => (
                                        <ChannelCard
                                            key={channel.ChannelName}
                                            channel={channel}
                                            isExpanded={isExpanded(channel.ChannelName)}
                                            isLiveGroup={true}
                                            toggleInfo={toggleInfo}
                                            abrirCanal={abrirCanal}
                                            abrirCanalOnStreams={abrirCanalOnStreams}
                                            abrirCanalOnDemand={abrirCanalOnDemand}
                                            navigateYouTube={navigateYouTube}
                                        />
                                    ))}
                        </div>
                    </div>
                )}

                {/* GRUPO ON DEMAND */}
                {canalesOnDemand.length > 0 && (
                    <div className="status-group">
                        <div className="videostatus">
                            <span className="videostatusspan">ONDEMAND</span>
                            <div className="videostatusend"></div>
                        </div>
                        <div className="cards-container">
                                    {canalesOnDemand.map(channel => (
                                        <ChannelCard
                                            key={channel.ChannelName}
                                            channel={channel}
                                            isExpanded={isExpanded(channel.ChannelName)}
                                            isLiveGroup={false}
                                            toggleInfo={toggleInfo}
                                            abrirCanal={abrirCanal}
                                            abrirCanalOnStreams={abrirCanalOnStreams}
                                            abrirCanalOnDemand={abrirCanalOnDemand}
                                            navigateYouTube={navigateYouTube}
                                        />
                                    ))}
                        </div>
                    </div>
                )}
                </div>

                <button className={`scroll-arrow right-arrow ${!canScrollRight ? 'disabled' : ''}`} onClick={() => scrollRef.current?.scrollBy({ left: window.innerWidth / 2, behavior: 'smooth' })}>›</button>
            </div>
        </div>
    );
};
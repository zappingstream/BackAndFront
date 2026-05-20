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
    abrirCanalOnDemand: (channel: Channel) => void;
    navigateYouTube: (url: string) => void;
}

export const ChannelCategoryRow = ({
    title,
    channels,
    expandedChannels,
    toggleInfo,
    abrirCanal,
    abrirCanalOnDemand,
    navigateYouTube,
}: ChannelCategoryProps) => {
    const scrollRef = useHorizontalScroll();

    if (!channels.length) return null;

    const canalesEnVivo = channels.filter(c => c.ChannelLive || (c.Actives && Object.keys(c.Actives).length > 0));
    const canalesOnDemand = channels.filter(c => !c.ChannelLive && (!c.Actives || Object.keys(c.Actives).length === 0));

    const isExpanded = (name: string) => expandedChannels.has(name);

    return (
        <div key={title}>
            <h2 className="category-title">{title}</h2>
            <div className="channel-row" ref={scrollRef}>

                {/* GRUPO EN VIVO */}
                {canalesEnVivo.length > 0 && (
                    <div className="status-group">
                        <div className="videostatus">
                            <span className="videostatusspan">AHORA</span>
                            <div className="videostatusend"></div>
                        </div>
                        <div className="cards-container">
                                    {canalesEnVivo.map((channel, idx) => (
                                        <ChannelCard
                                            key={idx}
                                            channel={channel}
                                            isExpanded={isExpanded(channel.ChannelName)}
                                            isLiveGroup={true}
                                            toggleInfo={toggleInfo}
                                            abrirCanal={abrirCanal}
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
                                    {canalesOnDemand.map((channel, idx) => (
                                        <ChannelCard
                                            key={idx}
                                            channel={channel}
                                            isExpanded={isExpanded(channel.ChannelName)}
                                            isLiveGroup={false}
                                            toggleInfo={toggleInfo}
                                            abrirCanal={abrirCanal}
                                            abrirCanalOnDemand={abrirCanalOnDemand}
                                            navigateYouTube={navigateYouTube}
                                        />
                                    ))}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};
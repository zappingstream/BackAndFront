import { useState, useMemo, useEffect, useRef, type RefObject } from 'react';
import type { Channel, UpcomingVideo, ActiveVideo } from '../models/Channel';
import { VideoCard } from './VideoCard';
import { ChannelCard } from './ChannelCard';
import { useHorizontalScroll } from '../hooks/useHorizontalScroll';
import './ScheduleGrid.css';

const EpgTrack = ({ row, navigateYouTube }: { row: any, navigateYouTube: (url: string) => void }) => {
    const trackRef = useHorizontalScroll();

    return (
        <div className="epg-events-track" ref={trackRef}>
            {row.events.map((ev: any, eIdx: number) => {
                const exactDate = new Date(ev.ScheduledStartTime);
                const timeStr = isNaN(exactDate.getTime()) ? "??:??" : exactDate.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false });
                const isPastEvent = ev.IsPast && !ev.Live;

                return (
                    <div key={eIdx} className={`epg-card ${ev.Live ? 'is-live-card' : ''}`}>
                        <div className="epg-card-inner" tabIndex={0} onClick={() => navigateYouTube(`https://www.youtube.com/watch?v=${ev.VideoId}`)}>
                            <div className="timeline-channel-header epg-card-header">
                                <span className={`time-badge epg-time-badge ${isPastEvent ? 'past-time-badge' : ''}`}>{timeStr}</span>
                            </div>
                            <VideoCard
                                className="primary-video"
                                imageUrl={ev.ThumbnailUrl || ev.channel.ChannelImgUrl}
                                altText={ev.Title}
                                fallbackText={ev.channel.ChannelName}
                                isLive={ev.Live}
                                isPremiere={ev.IsPremiere}
                                isPast={isPastEvent}
                                isUpcoming={!ev.IsPast && !ev.Live}
                            />
                            <div className="event-info">
                                <div className="event-title" title={ev.Title}>{ev.Title}</div>
                            </div>
                        </div>
                    </div>
                );
            })}
        </div>
    );
};

interface ScheduleGridProps {
    channels: Channel[];
    navigateYouTube: (url: string) => void;
    expandedChannels: Set<string>;
    toggleInfo: (channelName: string) => void;
    abrirCanal: (channel: Channel) => void;
    abrirCanalOnStreams: (channel: Channel) => void;
    abrirCanalOnDemand: (channel: Channel) => void;
    onRefresh: () => void;
    isRefreshing: boolean;
}

export const ScheduleGrid = ({ 
    channels, 
    navigateYouTube,
    expandedChannels,
    toggleInfo,
    abrirCanal,
    abrirCanalOnStreams,
    abrirCanalOnDemand,
}: ScheduleGridProps) => {
    // Generar la lista de 15 días (-7 a +7)
    const days = useMemo(() => {
        const daysList = [];
        for (let i = -7; i <= 7; i++) {
            const d = new Date();
            d.setDate(d.getDate() + i);
            daysList.push(d);
        }
        return daysList;
    }, []);
    
    const today = useMemo(() => new Date(), []);
    const [selectedDate, setSelectedDate] = useState<Date>(today);
    const daysRailRef = useRef<HTMLDivElement>(null);
    useHorizontalScroll<HTMLDivElement>(daysRailRef as RefObject<HTMLDivElement>);

    const { channelRows } = useMemo(() => {
        const rows: { channel: Channel, events: any[], TotalLanes: number }[] = [];

        channels.forEach(channel => {
            const events: any[] = [];

            const checkAndAddEvent = (v: UpcomingVideo | ActiveVideo, isForceLive: boolean = false) => {
                const isLive = v.Live || isForceLive;
                const effectiveStartTime = v.ActualStartTime || v.ScheduledStartTime || v.AddedAt;
                
                if (effectiveStartTime) {
                    const vDate = new Date(effectiveStartTime);
                    if (vDate.getFullYear() === selectedDate.getFullYear() &&
                        vDate.getMonth() === selectedDate.getMonth() &&
                        vDate.getDate() === selectedDate.getDate()) {
                        
                        if (!events.some(e => e.VideoId === v.VideoId)) {
                            events.push({ ...v, ScheduledStartTime: effectiveStartTime, Live: isLive, channel });
                        }
                    }
                }
            };

            if (channel.Upcoming) Object.values(channel.Upcoming).filter(v => v.ScheduledStartTime).forEach(v => checkAndAddEvent(v, false));
            if (channel.Actives) Object.values(channel.Actives).forEach(v => checkAndAddEvent(v, true));
            
            if (channel.Past) {
                Object.values(channel.Past).forEach(v => {
                    const effectiveStartTime = v.ActualStartTime || v.ScheduledStartTime || (v as any).AddedAt;
                    if (!effectiveStartTime) return;
                    const vDate = new Date(effectiveStartTime);
                    if (!isNaN(vDate.getTime()) && 
                        vDate.getFullYear() === selectedDate.getFullYear() &&
                        vDate.getMonth() === selectedDate.getMonth() &&
                        vDate.getDate() === selectedDate.getDate()) {
                        
                        if (!events.some(e => e.VideoId === v.VideoId)) {
                            events.push({
                                ...v,
                                ScheduledStartTime: effectiveStartTime,
                                Live: false,
                                IsPast: true,
                                channel
                            });
                        }
                    }
                });
            }

            if (channel.ChannelLive && channel.LiveVideoId) {
                const alreadyExists = events.some(e => e.VideoId === channel.LiveVideoId);
                if (!alreadyExists) {
                    let realTitle = `${channel.ChannelName} en vivo`;
                    
                    const activeVid = channel.Actives ? Object.values(channel.Actives).find(v => v.VideoId === channel.LiveVideoId) : null;
                    const upcomingVid = channel.Upcoming ? Object.values(channel.Upcoming).find(v => v.VideoId === channel.LiveVideoId) : null;
                    
                    if (activeVid?.Title) realTitle = activeVid.Title;
                    else if (upcomingVid?.Title) realTitle = upcomingVid.Title;

                    const effectiveStart = activeVid?.ActualStartTime || activeVid?.ScheduledStartTime || activeVid?.AddedAt || upcomingVid?.ActualStartTime || upcomingVid?.ScheduledStartTime || upcomingVid?.AddedAt || new Date().toISOString();
                    const vDate = new Date(effectiveStart);

                    if (vDate.getFullYear() === selectedDate.getFullYear() &&
                        vDate.getMonth() === selectedDate.getMonth() &&
                        vDate.getDate() === selectedDate.getDate()) {
                            
                        events.push({
                        VideoId: channel.LiveVideoId,
                        Title: realTitle,
                            ScheduledStartTime: effectiveStart,
                        ThumbnailUrl: channel.ChannelImgLiveUrl || channel.ChannelImgUrl,
                        AddedAt: new Date().toISOString(),
                        Live: true,
                        IsPremiere: channel.IsPremiere,
                        channel
                    });
                    }
                }
            }

            if (events.length > 0) {
                const getTime = (dateStr?: string) => {
                    if (!dateStr) return new Date().getTime();
                    const t = new Date(dateStr).getTime();
                    return isNaN(t) ? new Date().getTime() : t;
                };
                events.sort((a, b) => getTime(a.ScheduledStartTime) - getTime(b.ScheduledStartTime));
                
                rows.push({ channel, events, TotalLanes: 1 });
            }
        });

        rows.sort((a, b) => a.channel.ChannelName.localeCompare(b.channel.ChannelName));

        return { channelRows: rows };
    }, [channels, selectedDate]);

    // Auto-scroll al día actual y alinear los vivos
    useEffect(() => {
        if (daysRailRef.current) {
            const selectedElement = daysRailRef.current.querySelector('.selected') as HTMLElement;
            if (selectedElement) {
                selectedElement.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
            }
        }

        const scrollTimeout = setTimeout(() => {
            const tracks = document.querySelectorAll('.epg-events-track');
            tracks.forEach(track => {
                const liveCard = track.querySelector('.is-live-card') as HTMLElement;
                if (liveCard) {
                    // getBoundingClientRect es muchísimo más exacto y no depende de flexbox u offsetParents
                    const trackRect = track.getBoundingClientRect();
                    const cardRect = liveCard.getBoundingClientRect();
                    
                    // Calculamos la posición exacta combinando el scroll actual y la distancia hacia el elemento
                    const targetScroll = track.scrollLeft + (cardRect.left - trackRect.left);
                    track.scrollTo({ left: targetScroll, behavior: 'smooth' });
                } else {
                    track.scrollTo({ left: 0, behavior: 'smooth' });
                }
            });
        }, 500);

        return () => clearTimeout(scrollTimeout);
    }, [selectedDate, channelRows]);

    const isToday = selectedDate.toDateString() === today.toDateString();
    const formatDay = (date: Date) => {
        const isToday = date.toDateString() === today.toDateString();
        if (isToday) return "Hoy";
        return date.toLocaleDateString('es-AR', { weekday: 'short', day: 'numeric', month: 'numeric' });
    };

    const hasAnyContent = channelRows.length > 0;

    return (
        <div className="schedule-container">
            <div className="days-rail" ref={daysRailRef}>
                {days.map((date, idx) => {
                    const isSelected = date.toDateString() === selectedDate.toDateString();
                    return (
                        <button
                            key={idx}
                            className={`day-btn ${isSelected ? 'selected' : ''}`}
                            onClick={() => setSelectedDate(date)}
                        >
                            {formatDay(date)}
                        </button>
                    );
                })}
            </div>

            <div className="epg-container">
                {!hasAnyContent ? (
                    <div className="no-events-msg">No hay transmisiones programadas para este día.</div>
                ) : (
                    <>
                        {channelRows.map((row, idx) => {
                            const isAllPast = !row.events.some((e: any) => e.Live) && (row.events.every((e: any) => e.IsPast) || (isToday && new Date(row.events[row.events.length - 1].ScheduledStartTime).getTime() < today.getTime()));

                            return (
                                <div key={`row-${idx}`} className={`timeline-row ${isAllPast ? 'past-row' : ''}`}>
                                    <div className="timeline-channel-sidebar">
                                        <img src={row.channel.ChannelImgUrl} alt={row.channel.ChannelName} className="sidebar-channel-logo" loading="lazy" />
                                        <span className="sidebar-channel-name">{row.channel.ChannelName}</span>
                                        <button className="toggle-info-btn toggle-info-btn-small" onClick={(e) => { e.stopPropagation(); toggleInfo(row.channel.ChannelName); }}>Info</button>
                                    </div>
                                    <EpgTrack row={row} navigateYouTube={navigateYouTube} />
                                </div>
                            );
                        })}
                    </>
                )}
            </div>

            {/* Renderizar los modales globalmente para evitar bugs de stacking context y transforms */}
            {Array.from(expandedChannels).map(channelName => {
                const channel = channels.find(c => c.ChannelName === channelName);
                if (!channel) return null;
                return (
                    <div key={`expanded-${channelName}`} className="expanded-overlay-container">
                        <ChannelCard
                            channel={channel}
                            isExpanded={true}
                            isLiveGroup={false}
                            toggleInfo={toggleInfo}
                            abrirCanal={abrirCanal}
                            abrirCanalOnStreams={abrirCanalOnStreams}
                            abrirCanalOnDemand={abrirCanalOnDemand}
                            navigateYouTube={navigateYouTube}
                        />
                    </div>
                );
            })}
        </div>
    );
};
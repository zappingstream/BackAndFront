import { useState, useMemo } from 'react';
import { useChannels } from './hooks/useChannels';
import type { Channel } from './models/Channel';
import { removeDiacritics } from './index';
import { AppHeader } from './components/AppHeader';
import { InfoModal } from './components/InfoModal';
import { ChannelCategoryRow } from './components/ChannelCategoryRow';
import { AppFooter } from './components/AppFooter';
import { ScheduleGrid } from './components/ScheduleGrid';
import { StatusDisplay } from './components/StatusDisplay';
import './global.css';
import './App.css';

export default function App() {
  const { channels, isLoading: isFetching, refetch } = useChannels();
  const [isLoading, setIsLoading] = useState(false);
  const [showInfoModal, setShowInfoModal] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [sortBy, setSortBy] = useState("actividad");
  const [expandedChannels, setExpandedChannels] = useState<Set<string>>(new Set());
  const [viewMode, setViewMode] = useState<'cards' | 'grid'>('cards');

  const filteredChannels = useMemo(() => {
    if (!channels) return [];
    if (!searchText.trim()) return channels;

    const cleanSearch = removeDiacritics(searchText.trim()).toLowerCase();
    return channels.filter(c => {
      const cleanName = removeDiacritics(c.ChannelName || "").toLowerCase();
      const cleanCity = removeDiacritics(c.ChannelCity || "").toLowerCase();
      return cleanName.includes(cleanSearch) || cleanCity.includes(cleanSearch);
    });
  }, [channels, searchText]);

  const sortChannels = (source: Channel[]) => {
    return [...source].sort((a, b) => {
      if (sortBy === "nombre") return a.ChannelName.localeCompare(b.ChannelName);
      return new Date(b.LastActivityAt).getTime() - new Date(a.LastActivityAt).getTime();
    });
  };

  const streams = sortChannels(filteredChannels.filter(c => c.ChannelType?.toLowerCase().includes("stream")));
  const radios = sortChannels(filteredChannels.filter(c => c.ChannelType?.toLowerCase().includes("radio")));
  const televisions = sortChannels(filteredChannels.filter(c => c.ChannelType?.toLowerCase().includes("television")));
  const personalStreams = sortChannels(filteredChannels.filter(c => c.ChannelType?.toLowerCase().includes("personal")));

  const navigateYouTube = (url: string) => window.open(url, '_blank', 'noopener,noreferrer');

  const abrirCanal = (canal: Channel) => {
    let urlDestino = canal.ChannelLiveUrl;
    const hasActives = canal.Actives && Object.keys(canal.Actives).length > 0;

    if (canal.ChannelLive || hasActives) {
      if (canal.IsPremiere && canal.LiveVideoId) {
        urlDestino = `https://www.youtube.com/watch?v=${canal.LiveVideoId}`;
      }
      if (urlDestino)
        navigateYouTube(urlDestino);
    } else {
      abrirCanalOnStreams(canal);
    }
  };

  const abrirCanalOnStreams = (canal: Channel) => {
    if (canal.ChannelLiveUrl) {
      navigateYouTube(canal.ChannelLiveUrl.replace("/live", "/streams"));
    }
  };

  const abrirCanalOnDemand = (canal: Channel) => {
    if (canal.ChannelLiveUrl) {
      navigateYouTube(canal.ChannelLiveUrl.replace("/live", ""));
    }
  };

  const toggleInfo = (channelName: string) => {
    setExpandedChannels(prev => {
      const newSet = new Set(prev);
      if (newSet.has(channelName)) {
        newSet.delete(channelName);
      } else {
        newSet.clear();
        newSet.add(channelName);
      }
      return newSet;
    });
  };

  const handleRefresh = async () => {
    setIsLoading(true);
    try {
      if (refetch) await refetch();
    } finally {
      setIsLoading(false);
    }
  };

  const showAppContent = !isFetching && !isLoading && channels.length > 0;

  return (
    <div className="zapping-container">
      <AppHeader
        searchText={searchText}
        onSearchChange={setSearchText}
        onRefresh={handleRefresh}
        isRefreshing={isLoading || isFetching}
        onShowInfo={() => setShowInfoModal(true)}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
      />

      {showInfoModal && <InfoModal onClose={() => setShowInfoModal(false)} />}

      <StatusDisplay
        isFetching={isFetching}
        isLoading={isLoading}
        hasChannels={channels.length > 0}
        hasFilteredChannels={filteredChannels.length > 0}
        searchText={searchText}
      />

      {showAppContent && viewMode === 'cards' && (
        <>
        <div> <br /></div>
          <div className="sort-container">
            <span className="videostatusspan sort-label">Ordenar Por </span>
            <select className="sort-select" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
              <option value="actividad">Última Actividad</option>
              <option value="nombre">Nombre del Canal</option>
            </select>
          </div>
          <ChannelCategoryRow title="Full Stream" channels={streams} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />
          <ChannelCategoryRow title="Radio" channels={radios} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />
          <ChannelCategoryRow title="Televisión" channels={televisions} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />
          <ChannelCategoryRow title="Personal" channels={personalStreams} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />
        </>
      )}

      {showAppContent && viewMode === 'grid' && (
        <ScheduleGrid
          channels={filteredChannels}
          navigateYouTube={navigateYouTube}
          expandedChannels={expandedChannels}
          toggleInfo={toggleInfo}
          abrirCanal={abrirCanal}
          abrirCanalOnStreams={abrirCanalOnStreams}
          abrirCanalOnDemand={abrirCanalOnDemand}
          onRefresh={handleRefresh}
          isRefreshing={isLoading || isFetching}
        />
      )}

      {!isFetching && <AppFooter />}

      {expandedChannels.size > 0 && (
        <div className="fullscreen-overlay" onClick={() => setExpandedChannels(new Set())}></div>
      )}
    </div>
  );
}
import { useState, useMemo, useEffect } from 'react';
import { useChannels } from './hooks/useChannels';
import { useLocations } from './hooks/useLocations';
import type { Channel } from './models/Channel';
import { removeDiacritics } from './index';
import { AppHeader } from './components/AppHeader';
import { InfoModal } from './components/InfoModal';
import { ChannelCategoryRow } from './components/ChannelCategoryRow';
import { ScheduleGrid } from './components/ScheduleGrid';
import { AppFooter } from './components/AppFooter';
import { StatusDisplay } from './components/StatusDisplay';
import './global.css';
import './App.css';

export default function App() {
  const { channels, isLoading: isFetching, refetch } = useChannels();
  const { cityProvinceMap, provinces } = useLocations();
  const [isLoading, setIsLoading] = useState(false);
  const [showInfoModal, setShowInfoModal] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [sortBy, setSortBy] = useState("actividad");
  const [expandedChannels, setExpandedChannels] = useState<Set<string>>(new Set());
  const [viewMode, setViewMode] = useState<'cards' | 'grid'>('cards');
  const [selectedProvince, setSelectedProvince] = useState<string>("");
  const [selectedCity, setSelectedCity] = useState<string>("");

  // Restablecer el scroll al principio al cambiar de pestaña
  useEffect(() => {
    window.scrollTo(0, 0);
  }, [viewMode]);

  const availableCities = useMemo(() => {
    if (!selectedProvince) {
       return Array.from(new Set(channels.map(c => c.ChannelCity).filter(Boolean))) as string[];
    }
    return Array.from(new Set(
      channels
        .filter(c => c.ChannelCity && cityProvinceMap[c.ChannelCity] === selectedProvince)
        .map(c => c.ChannelCity)
    )) as string[];
  }, [channels, selectedProvince, cityProvinceMap]);

  const filteredChannels = useMemo(() => {
    if (!channels) return [];
    
    let result = channels;

    if (selectedProvince) {
        result = result.filter(c => c.ChannelCity && cityProvinceMap[c.ChannelCity] === selectedProvince);
    }

    if (selectedCity) {
        result = result.filter(c => c.ChannelCity === selectedCity);
    }

    if (searchText.trim()) {
      const cleanSearch = removeDiacritics(searchText.trim()).toLowerCase();
      result = result.filter(c => {
        const cleanName = removeDiacritics(c.ChannelName || "").toLowerCase();
        return cleanName.includes(cleanSearch);
      });
    }

    return result;
  }, [channels, searchText, selectedProvince, selectedCity, cityProvinceMap]);

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
    const activeVideos = canal.Actives ? Object.values(canal.Actives) : [];
    
    if (activeVideos.length > 0) {
      activeVideos.sort((a, b) => {
        // Precedencia: Vivo (no estreno) por sobre Estreno
        if (a.IsPremiere && !b.IsPremiere) return 1;
        if (!a.IsPremiere && b.IsPremiere) return -1;

        const timeA = new Date(a.ActualStartTime || a.ScheduledStartTime || a.AddedAt || 0).getTime();
        const timeB = new Date(b.ActualStartTime || b.ScheduledStartTime || b.AddedAt || 0).getTime();
        return timeB - timeA;
      });
      const mainActive = activeVideos[0];
      if (!mainActive.IsPremiere && canal.ChannelLiveUrl) {
        navigateYouTube(canal.ChannelLiveUrl);
      } else {
        navigateYouTube(`https://www.youtube.com/watch?v=${mainActive.VideoId}`);
      }
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

  const handleProvinceChange = (prov: string) => {
    setSelectedProvince(prov);
    setSelectedCity("");
  };

  return (
    <div className="zapping-container">
      <AppHeader
        searchText={searchText}
        onSearchChange={setSearchText}
        onRefresh={handleRefresh}
        isRefreshing={isLoading || isFetching}
        viewMode={viewMode}
        onViewModeChange={setViewMode}
        selectedProvince={selectedProvince}
        onProvinceChange={handleProvinceChange}
        selectedCity={selectedCity}
        onCityChange={setSelectedCity}
        provinces={provinces}
        cities={availableCities}
      />

      {showInfoModal && <InfoModal onClose={() => setShowInfoModal(false)} />}

      <StatusDisplay
        isFetching={isFetching}
        isLoading={isLoading}
        hasChannels={channels.length > 0}
        hasFilteredChannels={filteredChannels.length > 0}
        searchText={searchText}
        hasActiveFilters={!!(selectedProvince || selectedCity)}
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
          {streams.length > 0 && <ChannelCategoryRow title="Full Stream" channels={streams} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />}
          {radios.length > 0 && <ChannelCategoryRow title="Radio" channels={radios} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />}
          {televisions.length > 0 && <ChannelCategoryRow title="Televisión" channels={televisions} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />}
          {personalStreams.length > 0 && <ChannelCategoryRow title="Personal" channels={personalStreams} {...{ expandedChannels, toggleInfo, abrirCanal, abrirCanalOnStreams, abrirCanalOnDemand, navigateYouTube }} />}
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

      {!isFetching && (
        <AppFooter 
          onRefresh={handleRefresh} 
          isRefreshing={isLoading || isFetching} 
          onShowInfo={() => setShowInfoModal(true)} 
        />
      )}

      {expandedChannels.size > 0 && (
        <div className="fullscreen-overlay" onClick={() => setExpandedChannels(new Set())}></div>
      )}
    </div>
  );
}
import { backendApi } from "../../backend";
import { GistCard } from "../GistCard";
import { GistEndCard } from "../GistEndCard";
import { useAppDispatch, useAppSelector } from "../../store";
import { lastGistChanged, selectDisabledFeedIds, selectLastGist, selectSearchQuery, selectTags } from "../slice";
import { Gist } from "src/types";
import { GistViewerBody } from "../GistViewerBody";
import { ErrorMessage } from "../ErrorMessage";
import { useEffect, useRef } from "react";
import { LoadingBar } from "../LoadingBar";


const FindLastGistId = (data: Gist[] | undefined) => {
  const ids = data?.map((gist: Gist) => gist.id)
  return ids == undefined || ids.length == 0 ? undefined : Math.min(...ids);
};

const SortByUpdatedDateDesc = (data: Gist[]) => {
  const sorted = data.slice();
  sorted.sort((a: Gist, b: Gist) => 
    new Date(b.updated).valueOf() - new Date(a.updated).valueOf());
  return sorted;
};

export const GistList = () => {
  const dispatch = useAppDispatch();
  const lastGist = useAppSelector(selectLastGist);
  const searchQuery = useAppSelector(selectSearchQuery);
  const tags = useAppSelector(selectTags);
  const disabledFeeds = useAppSelector(selectDisabledFeedIds);

  const { data, error, isFetching } = backendApi.useGetGistsQuery({ lastGist, searchQuery, tags, disabledFeeds });

  const viewerBodyRef = useRef<HTMLDivElement>(null);

  const handleScroll = () => {
    const element = viewerBodyRef.current;
    if (!element) return;
    const scrollPos = element.scrollTop + element.offsetHeight;
    const maxScroll = element.scrollHeight;
    if (scrollPos >= maxScroll - 200) {
      dispatch(lastGistChanged(FindLastGistId(data)));
    }
  }; 

  useEffect(() => {
    const element = viewerBodyRef.current
    if (!element) return; 
    viewerBodyRef.current.addEventListener("scroll", handleScroll);
    return () => element.removeEventListener("scroll", handleScroll);
  }, [lastGist, searchQuery, tags, isFetching]);

  if (error || data == undefined && !isFetching) {
    return <GistViewerBody>
      <ErrorMessage />
    </GistViewerBody>
  }

  const sortedData = data ? SortByUpdatedDateDesc(data) : [];

  const dataToDisplay = sortedData.map((gist, i) => (
    <GistCard key={i} gist={gist} />
  ));

  dataToDisplay.push(isFetching 
    ? <LoadingBar key={dataToDisplay.length} /> 
    : <GistEndCard key={dataToDisplay.length} />
  );

  return <GistViewerBody scrollRef={ viewerBodyRef }>
    { dataToDisplay }
  </GistViewerBody>;
};

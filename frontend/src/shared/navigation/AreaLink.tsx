/**
 * @input The props of a router link.
 * @output A router link that loads the page when its target belongs to the other entry (ADR-0038).
 * @position Link component of Astryx (`AstryxProvider`).
 */
import { Link, useHref, useLocation, type LinkProps } from 'react-router';

import { isAdminPath } from './area';

const PATH_END = /[?#]/;

export function AreaLink(props: LinkProps) {
  const { pathname } = useLocation();
  const target = useHref(props.to, { relative: props.relative }).split(PATH_END)[0] ?? '';
  return <Link {...props} reloadDocument={props.reloadDocument ?? isAdminPath(pathname) !== isAdminPath(target)} />;
}
